using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// PixelVfxWorkQueue에 등록하는 이펙트가 구현한다. 한 프레임의 일을 LateUpdate(유니티 상태 읽기, 난수 뽑기처럼 순서가 중요한 일),
    /// ComputeDeferred(유니티 API를 쓰지 않는 계산), ApplyDeferred(메쉬/렌더러 반영)로 나눈다.
    /// </summary>
    internal interface IPixelVfxDeferred
    {
        /// <summary>
        /// 일꾼 스레드에서 불릴 수 있다. 이 인스턴스의 데이터와 스레드별 작업 버퍼만 읽고 쓴다. 유니티 API(Time, Transform, Renderer, Mesh 등)와
        /// 다른 인스턴스와 공유하는 변경 가능한 정적 데이터는 쓰면 안 된다.
        /// </summary>
        void ComputeDeferred();

        /// <summary>주 스레드에서 ComputeDeferred가 끝난 뒤 등록 순서대로 불린다. 계산 결과를 메쉬/렌더러에 반영한다.</summary>
        void ApplyDeferred();
    }

    /// <summary>
    /// 절차적 픽셀 이펙트가 매 프레임 하는 일 가운데 유니티 API를 쓰지 않는 계산(입자 이동, 픽셀 채우기)을 일꾼 스레드에 나눠 돌리는 대기열.
    ///
    /// 흐름: 이펙트는 LateUpdate에서 예전과 같은 시점에 유니티 상태를 읽고 자신을 등록한다(Enqueue). 모든 스크립트의 LateUpdate가 끝난 직후
    /// (PreLateUpdate.ScriptRunBehaviourLateUpdate 바로 뒤에 끼운 단계) 등록된 계산을 유니티 잡 일꾼에 나눠 돌리고, 끝나면 주 스레드에서 등록 순서대로 반영한다.
    ///
    /// 결과가 예전(각자 LateUpdate에서 바로 계산)과 같은 이유:
    /// - 계산은 인스턴스마다 독립이다(자기 데이터와 스레드별 작업 버퍼만 쓴다). 그래서 어느 스레드에서 어떤 순서로 돌아도 인스턴스별 결과가 같다.
    /// - 계산이 읽는 유니티 상태(시간, 위치, 카메라 절두체)는 LateUpdate에서 예전과 같은 시점에 읽어 인스턴스에 담아 둔다.
    /// - 반영(메쉬 올리기)은 같은 프레임 렌더링 전에 끝난다. 그 사이에 이 값을 읽는 코드는 없다.
    /// - 등록한 뒤 같은 프레임에 인스턴스가 꺼지거나 다시 시작되면 그 전에 CompleteNow로 그 인스턴스의 계산과 반영을 먼저 끝낸다(예전 순서와 같다).
    ///
    /// 플레이 중이 아니거나 끼운 단계가 없으면 Enqueue가 그 자리에서 바로 계산과 반영을 한다(예전과 같은 동작).
    /// </summary>
    internal static class PixelVfxWorkQueue
    {
        // 이보다 적으면 일꾼을 깨우는 비용이 더 커서 주 스레드에서 바로 계산한다
        private const int MinParallelCount = 4;

        private struct FlushStep
        {
        }

        private struct ComputeJob : IJobParallelFor
        {
            public void Execute(int _index)
            {
                IPixelVfxDeferred item = items[_index];
                if (null != item) item.ComputeDeferred();
            }
        }

        private static IPixelVfxDeferred[] items = new IPixelVfxDeferred[256];
        private static int count;
        private static int serial;
        private static bool bInstalled;
        private static bool bFlushing;
        private static bool bApplying;

        /// <summary>
        /// 계산 묶음 번호. 한 번의 일괄 처리(Flush)나 단독 처리(CompleteNow, 즉시 실행)마다 바뀐다. 스레드별 작업 버퍼는 이 값이 바뀌었을 때 비우면 된다
        /// (번호가 바뀌는 시점에는 앞선 계산 결과가 모두 반영을 마친 뒤다).
        /// </summary>
        public static int Serial => serial;

        /// <summary>
        /// 이번 프레임 계산을 등록한다. 돌려준 자리 번호는 CompleteNow에 넘긴다. 바로 실행했으면(-1) ApplyDeferred까지 끝난 상태다.
        /// </summary>
        public static int Enqueue(IPixelVfxDeferred _item)
        {
            if (false == bInstalled || true == bFlushing || false == Application.isPlaying)
            {
                ExecuteNow(_item);
                return -1;
            }

            if (count == items.Length) Array.Resize(ref items, count * 2);
            items[count] = _item;
            return count++;
        }

        /// <summary>
        /// 등록하지 않고 지금 주 스레드에서 계산과 반영을 한다. 미루면 예전과 시점이 달라질 수 있는 경우(이펙트가 판단한다)에 쓴다.
        /// </summary>
        public static void ExecuteNow(IPixelVfxDeferred _item)
        {
            unchecked { serial++; }
            _item.ComputeDeferred();
            _item.ApplyDeferred();
        }

        /// <summary>
        /// 등록해 둔 한 건을 지금 주 스레드에서 끝낸다(계산 + 반영). 인스턴스가 꺼지거나 다시 시작되기 전에 불러 예전 순서(계산 뒤 끄기)를 지킨다.
        /// </summary>
        public static void CompleteNow(int _slot, IPixelVfxDeferred _item)
        {
            if (0 > _slot || _slot >= count || false == ReferenceEquals(items[_slot], _item)) return;

            items[_slot] = null;
            if (false == bApplying)
            {
                unchecked { serial++; }
                _item.ComputeDeferred();
            }

            _item.ApplyDeferred();
        }

        private static void Flush()
        {
            if (0 == count || true == bFlushing) return;

            bFlushing = true;
            int total = count;
            try
            {
                unchecked { serial++; }
                if (MinParallelCount <= total)
                {
                    default(ComputeJob).Schedule(total, 1).Complete();
                }
                else
                {
                    for (int i = 0; i < total; i++)
                    {
                        IPixelVfxDeferred item = items[i];
                        if (null != item) item.ComputeDeferred();
                    }
                }

                bApplying = true;
                for (int i = 0; i < total; i++)
                {
                    IPixelVfxDeferred item = items[i];
                    if (null == item) continue;

                    items[i] = null;
                    try
                    {
                        item.ApplyDeferred();
                    }
                    catch (Exception e)
                    {
                        // 한 인스턴스의 오류가 나머지의 반영을 막지 않게 한다(예전에는 각자 LateUpdate라 서로 영향이 없었다)
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < total; i++) items[i] = null;
                count = 0;
                bApplying = false;
                bFlushing = false;
            }
        }

        // 끼운 단계가 돌지 못한 프레임(다른 코드가 게임 루프를 통째로 바꾼 경우)에도 그리기 전에는 반드시 끝낸다
        private static void OnBeginContextRendering(ScriptableRenderContext _context, List<Camera> _cameras)
        {
            if (0 < count) Flush();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            for (int i = 0; i < items.Length; i++) items[i] = null;
            count = 0;
            bFlushing = false;
            bApplying = false;

            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;

            // 스크립트 LateUpdate 단계 바로 뒤에 일괄 처리 단계를 끼운다(이미 끼워져 있으면 - 도메인 리로드 없이 다시 플레이 - 바꿔 끼운다)
            bInstalled = false;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (null == loop.subSystemList) return;

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                PlayerLoopSystem phase = loop.subSystemList[i];
                if (typeof(PreLateUpdate) != phase.type || null == phase.subSystemList) continue;

                List<PlayerLoopSystem> list = new List<PlayerLoopSystem>(phase.subSystemList);
                list.RemoveAll(s => typeof(FlushStep) == s.type);
                int at = list.FindIndex(s => typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate) == s.type);
                if (0 > at) return;

                list.Insert(at + 1, new PlayerLoopSystem { type = typeof(FlushStep), updateDelegate = Flush });
                phase.subSystemList = list.ToArray();
                loop.subSystemList[i] = phase;
                PlayerLoop.SetPlayerLoop(loop);
                bInstalled = true;
                return;
            }
        }
    }
}
