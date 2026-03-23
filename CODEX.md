## 언어 및 소통 설정
- 모든 답변과 코드 설명은 **한국어**로 작성해줘.

## 코딩 컨벤션 가이드 (C# / Unity)

이 프로젝트의 비서로서 다음의 코딩 규칙을 엄격히 준수하세요.

### 1. 명명 규칙
- **클래스/메서드/인터페이스:** `PascalCase`
- **프라이빗,퍼블릭 필드:** `camelCase` (언더바 없음)
- **메서드 매개변수:** `_camelCase` (언더바 접두어 필수)

### 2. 클래스 구조 순서
1. 필드 선언 (주석으로 //외부 의존성, //내부 의존성 구분)
2. 퍼블릭 초기화 및 제어 메서드 (Initialize, Setup, Release 등)
3. 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

### 3. 설계 패턴
- **수동 의존성 주입:** `Initialize(_dependency)` 패턴을 사용하여 객체 간 결합도를 관리합니다.
- **컴포넌트 할당:** 가능한 경우 `GetComponentInChildren` 등을 사용하여 Initialize 시점에 내부 참조를 구성합니다.
- **명시성:** 모든 멤버에 접근 제어자를 생략하지 말고 반드시 작성하세요.

## 🛠 메모리 및 성능 최적화 원칙 (Unity C#)

우리 프로젝트는 60FPS 유지를 위해 GC(Garbage Collection) 발생과 메모리 단편화를 엄격히 제한한다. 모든 코드 생성 및 수정 시 다음 규칙을 반드시 준수하라.

### 1. GC 발생 최소화 (Minimize GC Alloc)
- **LINQ 사용 금지:** `Where`, `Select`, `ToList` 등 모든 LINQ 메서드는 힙 할당을 유발하므로 사용하지 않는다. 대신 `for` 또는 `foreach` 루프를 사용하라.
- **클래스 대신 구조체(struct) 활용:** 데이터 중심의 작은 객체는 `class` 대신 `struct`로 선언하여 스택 메모리를 활용하라. (단, 16바이트를 넘거나 잦은 복사가 발생하는 경우는 예외)
- **컬렉션 재사용 (Pooling):** `new List<T>()`를 루프 내에서 생성하지 마라. 멤버 변수로 선언하고 `Clear()`를 호출하여 재사용하거나 `ArrayPool<T>`을 활용하라.
- **문자열 연산 주의:** 루프 내에서 문자열 더하기(`+`)를 금지한다. 빈번한 문자열 조립이 필요하면 `StringBuilder`를 사용하라.
- **클로저(Closure) 주의:** 람다식 내부에서 외부 변수를 참조하면 할당이 발생한다. 가급적 정적 메서드나 인자를 전달하는 방식을 사용하라.
- **문자열 사전 캐싱:** 빈번히 사용되는 문자열(태그명, UI 텍스트 등)은 멤버 변수에 미리 할당하여 런타임 생성을 방지하라.
- **초기 용량 설정:** 컬렉션 생성 시 예상 최대 크기(Capacity)를 지정하라.

### 2. 메모리 단편화 방지 (Minimize Fragmentation)
- **컬렉션 초기 용량 설정:** `List`, `Dictionary` 등을 생성할 때 예상되는 최대 크기를 생성자 인자로 넘겨(`Capacity`) 내부 배열의 잦은 재할당과 복사를 방지하라.
- **고정 크기 버퍼 활용:** 자주 사용하는 임시 버퍼는 `Span<T>` 또는 `stackalloc`을 사용하여 힙 할당 없이 처리하라.
- **Boxing/Unboxing 제거:** 제네릭을 적극 활용하여 값 형식이 `object`로 변환되는 과정을 차단하라. (특히 `Enum`을 키로 사용하는 Dictionary 주의)

### 3. 유니티 전용 최적화
- **캐싱(Caching):** `GetComponent`, `Camera.main`, `new WaitForSeconds` 등은 멤버 변수에 캐싱하여 매 프레임 호출을 방지하라.
- **Physics API 주의:** `RaycastAll` 대신 `RaycastNonAlloc`을, `OverlapSphere` 대신 `OverlapSphereNonAlloc`을 사용하여 결과 배열 할당을 피하라.
- **문자열 비교:** 태그 비교 시 `go.tag == "Player"` 대신 `go.CompareTag("Player")`를 사용하라.

### 4. 스택 메모리 활용 극대화 (Stack-First Memory Management)
- **구조체 참조 전달:** 구조체를 메서드 인자로 전달할 때 복사 비용을 줄이기 위해 in (읽기 전용 참조) 또는 ref 키워드를 사용하라.
- **로컬 함수 및 정적 메서드:** 힙 할당을 유발하는 델리게이트 생성을 피하기 위해 가능한 경우 로컬 함수나 static 메서드를 정의하여 사용하라.
- **값 형식 캐스팅 방지:** interface를 통한 구조체 접근은 박싱을 유발하므로, 제네릭 제약 조건(where T : struct)을 사용하여 직접 타입을 다루도록 하라.
- **값 형식(Struct) 우선:** 데이터 중심 객체는 class 대신 struct로 선언하여 스택 메모리를 활용하라.
- **stackalloc 및 Span<T>:** 함수 내부의 단기 임시 버퍼는 힙 대신 stackalloc을 사용하여 스택에 할당하라.