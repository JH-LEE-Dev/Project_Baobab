/// <summary>
/// 프로젝트 공용 오브젝트 풀 설정.
///
/// UnityEngine.Pool.ObjectPool의 collectionCheck는 Release마다 내부 Stack을 선형 탐색해
/// 이중 반납을 검사한다(Stack&lt;T&gt;.Contains = O(n)). 던전 전환처럼 수천 개를 한꺼번에
/// 반납하는 경로에서는 이게 O(N²)가 되어(나무 2500그루 기준 약 310만 회 비교) 전환 프레임을
/// 눌러버린다.
///
/// 그래서 에디터·개발 빌드에서는 켜 두어 이중 반납을 즉시 예외로 잡고, 릴리즈 빌드에서는 끈다.
/// 릴리즈에서의 안전망은 각 풀 대상이 들고 있는 IsPooled 플래그가 대신한다. 그쪽은 O(1)이라
/// 항상 켜져 있어도 비용이 없다.
/// </summary>
public static class PoolSettings
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const bool CollectionCheck = true;
#else
    public const bool CollectionCheck = false;
#endif
}
