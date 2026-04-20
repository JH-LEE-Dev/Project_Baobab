using UnityEngine;
using DG.Tweening;
using System;

[Serializable]
public class ObjectMotionData
{
    public enum MotionType { MoveAnchored, MovePosition, Scale, Rotate, Fade, End };
    public enum SequenceType { Append, Join }

    [Header("Events (Optional)")]
    public string startEventKey;    // 시작 시 호출할 이벤트 키
    public string startParam;       // 시작 시 호출할 이벤트의 파라미터
    public string completeEventKey; // 종료 시 호출할 이벤트 키
    public string completeParam;    // 종료 시 호출할 이벤트의 파라미터

    [Header("Sequence Settings")]
    public SequenceType sequenceType = SequenceType.Append; // 애니메이션 연결 방식

    [Header("Basic Settings")]
    public MotionType motionType = MotionType.End; // 애니메이션을 어떤 용도로 사용할 지
    public float duration;  // 애니메이션 재생 길이
    public float delay;     // 애니메이션 초반 딜레이
    public Ease ease;       // 애니메이션 이징 그래프

    [Header("Values")]
    public Vector3 targetVector; // Move나 Scale, Rotate 값 사용
    [Range(0, 1)] public float targetFloat; // fade ( 0 ~ 1 );

    [Header("Relative Motion - 현재 상태에서 모션 더하기")]
    public bool isRelative;
}
