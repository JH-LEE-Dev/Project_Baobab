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
    /// (PreLateUpdate.ScriptRunBehaviourLateUpdate 바로 뒤에 끼운 단계) 등록된 계산을 유니티 잡 일꾼에 넘긴다. 넘긴 자리에서 기다리지 않고
    /// 주 스레드는 엔진의 다음 단계(UI 캔버스, 소리)를 먼저 돌린 뒤, 파티클 종료 단계 직전(PostLateUpdate.ParticleSystemEndUpdateAll 앞에
    /// 끼운 단계)에 일꾼이 끝났는지 확인하고 등록 순서대로 반영한다("늦게 반영"). 일꾼을 깨워 끝나길 기다리던 고정 시간(프레임당 0.4~0.6ms)이
    /// 다른 일과 겹쳐 사라진다. 렌더러 갱신(PostLateUpdate.UpdateAllRenderers)과 그리기보다 앞이라 같은 프레임 그림에 반영된다.
    ///
    /// 결과가 예전(각자 LateUpdate에서 바로 계산)과 같은 이유:
    /// - 계산은 인스턴스마다 독립이다(자기 데이터와 스레드별 작업 버퍼만 쓴다). 그래서 어느 스레드에서 어떤 순서로 돌아도 인스턴스별 결과가 같다.
    /// - 계산이 읽는 유니티 상태(시간, 위치, 카메라 절두체)는 LateUpdate에서 예전과 같은 시점에 읽어 인스턴스에 담아 둔다.
    /// - 반영(메쉬 올리기)은 같은 프레임 렌더러 갱신 전에 끝난다. 그 사이에 도는 것은 UI 캔버스 갱신과 소리뿐이고 이 값을 읽지 않는다.
    ///   캔버스가 읽는 항목(HUD 도끼 오라)은 등록할 때 표시해 두면 넘기는 자리에서 주 스레드가 바로 계산·반영한다(캔버스보다 앞, 예전과 같은 프레임).
    /// - 등록한 뒤 같은 프레임에 인스턴스가 꺼지거나 다시 시작되면 그 전에 CompleteNow로 그 인스턴스의 계산과 반영을 먼저 끝낸다(예전 순서와 같다).
    ///   일꾼에 넘겨 둔 사이라면 일꾼이 끝나길 기다린 뒤 그 한 건만 먼저 반영한다. 이펙트 풀 반납(파티클 정지 콜백)은 파티클 종료 단계에서
    ///   일어나므로 반영이 끝난 뒤다.
    ///
    /// 이 대기열을 쓰는 이펙트가 지켜야 하는 것: 등록한 뒤에는 ComputeDeferred가 읽거나 쓰는 필드를 CompleteNow 없이 바꾸지 않는다
    /// (넘겨 둔 사이에 주 스레드가 바꾸면 일꾼과 동시에 건드리게 된다).
    ///
    /// 플레이 중이 아니거나 끼운 단계가 없으면 Enqueue가 그 자리에서 바로 계산과 반영을 한다(예전과 같은 동작).
    /// </summary>
    internal static class PixelVfxWorkQueue
    {
        // 이보다 적으면 일꾼을 깨우는 비용이 더 커서 주 스레드에서 바로 계산한다
        private const int MinParallelCount = 4;

        // 넘겨 둔 묶음의 상태
        private const int PendingNone = 0;       // 넘겨 둔 것이 없다
        private const int PendingCollecting = 1; // 넘기는 중(아직 일꾼에 넘기지 않아 남은 항목은 계산 전이다)
        private const int PendingScheduled = 2;  // 일꾼에 넘겼다(끝났는지는 잡 핸들로 확인한다)

        private struct FlushStep
        {
        }

        private struct LateApplyStep
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
        private static bool[] applyBeforeCanvas = new bool[256];
        private static int count;
        private static int serial;
        private static bool bInstalled;
        private static bool bLateInstalled;
        private static bool bFlushing;
        private static bool bApplying;
        private static int pendingState;
        private static int pendingTotal;
        private static JobHandle pendingHandle;

        /// <summary>
        /// 늦게 반영을 쓸지. 끄면 넘긴 자리에서 일꾼이 끝나길 기다렸다가 바로 반영한다(예전 방식, 문제가 생겼을 때의 비교·되돌리기용).
        /// 프레임 사이(넘겨 둔 것이 없을 때)에만 바꾼다.
        /// </summary>
        public static bool bLateApply = true;

        /// <summary>진단용: 직전에 넘긴 묶음의 항목 수.</summary>
        public static int LastBatchSize { get; private set; }

        /// <summary>진단용: 일꾼에 넘겨 둔 사이에 등록(Enqueue)이나 단독 처리(CompleteNow)가 끼어든 횟수. 평소에는 늘지 않아야 한다.</summary>
        public static int PendingInterruptCount { get; private set; }

        /// <summary>진단용: 지금 일꾼에 넘겨 둔 묶음이 있는지.</summary>
        public static bool IsPending => PendingNone != pendingState;

        /// <summary>
        /// 계산 묶음 번호. 한 번의 일괄 처리(Flush)나 단독 처리(CompleteNow, 즉시 실행)마다 바뀐다. 스레드별 작업 버퍼는 이 값이 바뀌었을 때 비우면 된다
        /// (번호가 바뀌는 시점에는 앞선 계산 결과가 모두 반영을 마친 뒤다). 계산해 둔 결과가 아직 반영 전인 동안에는 바뀌지 않는다.
        /// </summary>
        public static int Serial => serial;

        /// <summary>
        /// 이번 프레임 계산을 등록한다. 돌려준 자리 번호는 CompleteNow에 넘긴다. 바로 실행했으면(-1) ApplyDeferred까지 끝난 상태다.
        /// _bApplyBeforeCanvas: 반영 결과를 같은 프레임의 UI 캔버스 갱신이 읽는 항목(UI 그래픽)이면 true. 넘기는 자리에서 주 스레드가 바로 계산·반영한다.
        /// </summary>
        public static int Enqueue(IPixelVfxDeferred _item, bool _bApplyBeforeCanvas = false)
        {
            if (false == bInstalled || true == bFlushing || false == Application.isPlaying)
            {
                ExecuteNow(_item);
                return -1;
            }

            if (PendingNone != pendingState)
            {
                // 넘겨 둔 묶음이 반영되기 전에 새로 들어온 등록: 주 스레드에서 바로 계산·반영한다(묶음 번호는 바꾸지 않는다)
                PendingInterruptCount++;
                _item.ComputeDeferred();
                _item.ApplyDeferred();
                return -1;
            }

            if (count == items.Length)
            {
                Array.Resize(ref items, count * 2);
                Array.Resize(ref applyBeforeCanvas, count * 2);
            }

            items[count] = _item;
            applyBeforeCanvas[count] = _bApplyBeforeCanvas;
            return count++;
        }

        /// <summary>
        /// 등록하지 않고 지금 주 스레드에서 계산과 반영을 한다. 미루면 예전과 시점이 달라질 수 있는 경우(이펙트가 판단한다)에 쓴다.
        /// </summary>
        public static void ExecuteNow(IPixelVfxDeferred _item)
        {
            NextSerial();
            _item.ComputeDeferred();
            _item.ApplyDeferred();
        }

        /// <summary>
        /// 등록해 둔 한 건을 지금 주 스레드에서 끝낸다(계산 + 반영). 인스턴스가 꺼지거나 다시 시작되기 전에 불러 예전 순서(계산 뒤 끄기)를 지킨다.
        /// </summary>
        public static void CompleteNow(int _slot, IPixelVfxDeferred _item)
        {
            if (0 > _slot || _slot >= count || false == ReferenceEquals(items[_slot], _item)) return;

            if (true == bApplying)
            {
                // 계산은 이미 끝났다
                items[_slot] = null;
                _item.ApplyDeferred();
                return;
            }

            if (PendingScheduled == pendingState)
            {
                // 일꾼이 이 묶음을 계산하는 중일 수 있다. 끝나길 기다린 뒤 이 한 건만 먼저 반영한다(나머지는 제때 반영된다)
                PendingInterruptCount++;
                pendingHandle.Complete();
                items[_slot] = null;
                _item.ApplyDeferred();
                return;
            }

            items[_slot] = null;
            NextSerial();
            _item.ComputeDeferred();
            _item.ApplyDeferred();
        }

        // 계산해 둔 결과가 아직 반영되지 않은 동안에는 묶음 번호를 바꾸지 않는다(스레드별 작업 버퍼가 비워져 남은 결과가 깨진다)
        private static void NextSerial()
        {
            if (PendingNone != pendingState || true == bApplying) return;

            unchecked { serial++; }
        }

        private static void Flush()
        {
            // 지난 프레임에 넘겨 둔 묶음이 아직 남아 있으면(반영 단계도 그리기도 돌지 않은 프레임) 먼저 끝낸다
            if (PendingScheduled == pendingState) ApplyPending();
            if (0 == count || true == bFlushing || PendingNone != pendingState) return;

            LastBatchSize = count;
            if (true == bLateApply && true == bLateInstalled && MinParallelCount <= count)
            {
                FlushLate();
                return;
            }

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

        // 늦게 반영: 일꾼에 넘기기만 하고 돌아간다. 반영은 ApplyPending(끼운 단계)에서 한다.
        private static void FlushLate()
        {
            int total = count;
            unchecked { serial++; }
            pendingHandle = default;
            pendingTotal = total;
            pendingState = PendingCollecting;

            // 캔버스가 읽는 항목은 지금 주 스레드에서 끝낸다(캔버스 갱신이 반영 단계보다 먼저 돈다)
            int remaining = 0;
            for (int i = 0; i < total; i++)
            {
                IPixelVfxDeferred item = items[i];
                if (null == item) continue;

                if (false == applyBeforeCanvas[i])
                {
                    remaining++;
                    continue;
                }

                items[i] = null;
                try
                {
                    item.ComputeDeferred();
                    item.ApplyDeferred();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (MinParallelCount <= remaining)
            {
                pendingHandle = default(ComputeJob).Schedule(total, 1);
                pendingState = PendingScheduled;
                // 일꾼이 바로 시작하게 한다(안 하면 다음 Complete 때까지 미뤄질 수 있다)
                JobHandle.ScheduleBatchedJobs();
                return;
            }

            // 남은 것이 적으면 일꾼을 깨우지 않고 지금 끝낸다
            for (int i = 0; i < total; i++)
            {
                IPixelVfxDeferred item = items[i];
                if (null == item) continue;

                try
                {
                    item.ComputeDeferred();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            pendingState = PendingScheduled;
            ApplyPending();
        }

        // 넘겨 둔 묶음을 끝낸다: 일꾼이 끝났는지 확인하고(대개 이미 끝나 있다) 등록 순서대로 반영한다
        private static void ApplyPending()
        {
            if (PendingScheduled != pendingState) return;

            int total = pendingTotal;
            try
            {
                pendingHandle.Complete();
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
                pendingHandle = default;
                pendingState = PendingNone;
                bApplying = false;
            }
        }

        // 끼운 단계가 돌지 못한 프레임(다른 코드가 게임 루프를 통째로 바꾼 경우)이나 반영 단계보다 먼저 그리는 카메라가 있어도 그리기 전에는 반드시 끝낸다
        private static void OnBeginContextRendering(ScriptableRenderContext _context, List<Camera> _cameras)
        {
            if (PendingNone != pendingState) ApplyPending();
            if (0 < count)
            {
                Flush();
                if (PendingNone != pendingState) ApplyPending();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            for (int i = 0; i < items.Length; i++) items[i] = null;
            count = 0;
            bFlushing = false;
            bApplying = false;
            pendingHandle = default;
            pendingState = PendingNone;
            pendingTotal = 0;
            bLateApply = true;
            LastBatchSize = 0;
            PendingInterruptCount = 0;

            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;

            // 스크립트 LateUpdate 단계 바로 뒤에 일괄 처리 단계를, 파티클 종료 단계 바로 앞에 반영 단계를 끼운다
            // (이미 끼워져 있으면 - 도메인 리로드 없이 다시 플레이 - 바꿔 끼운다)
            bInstalled = false;
            bLateInstalled = false;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (null == loop.subSystemList) return;

            bool bFlushInserted = false;
            bool bApplyInserted = false;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                PlayerLoopSystem phase = loop.subSystemList[i];
                if (null == phase.subSystemList) continue;

                if (typeof(PreLateUpdate) == phase.type)
                {
                    List<PlayerLoopSystem> list = new List<PlayerLoopSystem>(phase.subSystemList);
                    list.RemoveAll(s => typeof(FlushStep) == s.type);
                    int at = list.FindIndex(s => typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate) == s.type);
                    if (0 <= at)
                    {
                        list.Insert(at + 1, new PlayerLoopSystem { type = typeof(FlushStep), updateDelegate = Flush });
                        bFlushInserted = true;
                    }

                    phase.subSystemList = list.ToArray();
                    loop.subSystemList[i] = phase;
                }
                else if (typeof(PostLateUpdate) == phase.type)
                {
                    List<PlayerLoopSystem> list = new List<PlayerLoopSystem>(phase.subSystemList);
                    list.RemoveAll(s => typeof(LateApplyStep) == s.type);
                    // 캔버스 갱신보다 뒤, 파티클 종료(풀 반납 콜백)와 렌더러 갱신보다 앞이어야 한다. 순서가 예상과 다르면 늦게 반영을 쓰지 않는다.
                    int canvases = list.FindIndex(s => typeof(PostLateUpdate.PlayerUpdateCanvases) == s.type);
                    int particles = list.FindIndex(s => typeof(PostLateUpdate.ParticleSystemEndUpdateAll) == s.type);
                    int renderers = list.FindIndex(s => typeof(PostLateUpdate.UpdateAllRenderers) == s.type);
                    if (0 <= canvases && canvases < particles && particles < renderers)
                    {
                        list.Insert(particles, new PlayerLoopSystem { type = typeof(LateApplyStep), updateDelegate = ApplyPending });
                        bApplyInserted = true;
                    }

                    phase.subSystemList = list.ToArray();
                    loop.subSystemList[i] = phase;
                }
            }

            if (false == bFlushInserted) return;

            PlayerLoop.SetPlayerLoop(loop);
            bInstalled = true;
            bLateInstalled = bApplyInserted;
        }
    }
}
