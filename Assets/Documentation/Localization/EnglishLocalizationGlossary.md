# Project Baobab English Localization Glossary

영문 현지화에서 같은 개념을 항상 같은 단어로 번역하기 위한 기준 문서다. 새 번역을 시작하기 전에 이 문서를 먼저 확인한다.

## 번역 기준

- `Assets/Resources/Localization`의 한국어 원문과 실제 게임 문맥을 기준으로 한다.
- `key`, `enumType`, `enumValue`는 개발용 식별자이므로 플레이어에게 보이는 이름의 근거로 삼지 않는다.
- 기존 영문이 이 용어집과 충돌하면 용어집의 표현으로 고친다.
- 직역보다 자연스러운 게임 UI 영어를 우선하되, 수치와 기능은 바꾸지 않는다.

## 문체 및 표기 규칙

- 능력명, 버튼, 메뉴, 라벨에는 끝 마침표를 붙이지 않는다.
- 완전한 문장으로 작성된 설명과 안내문에는 마침표를 붙인다.
- 질문과 감탄은 문맥에 맞게 `?`와 `!`를 사용한다.
- 능력명은 Title Case, 일반 설명문은 sentence case를 사용한다.
- `{0}` 같은 자리표시자와 `<COLOR>`, `<Wave>` 같은 마크업은 원문과 동일하게 보존한다.
- 퍼센트는 `{0}%`, 배수는 `{0}x`로 쓴다.
- 긴 설명은 대략 50~60자 안에서 `\n`으로 나누되, 관사와 명사 또는 형용사와 명사를 갈라놓지 않는다.
- `damage`를 사용하고 `DMG` 같은 임의 축약형은 사용하지 않는다.
- 고유 시스템명인 `Shockwave`, `Boomerang`, `Spore Shield`, `Spore Burst`, `Golden Tree`, `Star Mark`, `Constellation Strike`는 설명문 안에서도 대문자로 시작한다.

## 핵심 사물 및 시스템

| 한국어 | 고정 영문 | 사용하지 않을 표현 / 참고 |
|---|---|---|
| 원목 | log / logs | 일반 재화에는 `timber`, `wood` 사용 금지 |
| 원목 가치 | Log Value | 능력명에서는 Title Case |
| 원목 판매 가치 | sale value of logs | `log price`로 바꾸지 않음 |
| 희귀 원목 | rare log |  |
| 운반 상자 | cargo crate | 차량에 싣는 이동식 나무 상자 |
| 이동식 원목 운반 상자 | cargo crate | `transport container`, `transport crate` 사용 금지 |
| 제재소 원목 보관함 | log bin | 필요할 때 `the sawmill's log bin`으로 씀 |
| 제재소 | sawmill |  |
| 제재소 증설 | Sawmill Expansion | 능력명 |
| 원목 가공 속도 | log processing speed |  |
| 제재소 레일 속도 | sawmill conveyor speed | 능력명은 `Conveyor Speed` |
| 인벤토리 | inventory |  |
| 칸 | slot / slots | 수량 문맥에 맞게 사용 |
| 아이템 획득 범위 | item pickup range |  |
| 원목 넣기 / 꺼내기 | storing / retrieving logs |  |
| 원정 | expedition | `logging trip`은 벌목 원정 자체를 강조할 때만 사용 |
| 오프로드 차량 | off-road vehicle |  |
| 황금 나무 | Golden Tree | 고유 대상이므로 대문자 유지 |
| 나무 밀도 | tree density |  |
| 과성장 | Overgrowth | 능력명에서는 대문자 유지 |

## 아이템명

| 한국어 | 고정 영문 |
|---|---|
| 원목 보험 증서 | Log Insurance Policy |
| 포자 포션 | Spore Potion |
| 별 나침반 | Star Compass |
| 흑요석 부적 | Obsidian Charm |

## 지역명

| 한국어 | 고정 영문 | 금지 표기 |
|---|---|---|
| 풀빛너른숲 | Greenreach Forest | `WideGreen Forest`는 enum 직역이므로 사용 금지 |
| 몽글포자숲 | Puffspore Forest | `Fluffy Spore Forest` 사용 금지 |
| 별뿌리숲 / 별뿌리 숲 | Starroot Forest | `Star Root Forest` 사용 금지 |
| 용암숲 | Magma Forest | `Lava Forest`로 바꾸지 않음 |
| 마을 | town | 일반 명사이므로 문장 중간에서는 소문자 |

## 나무 종 이름

| 한국어 | 고정 영문 |
|---|---|
| 참나무 | Oak |
| 소나무 | Pine |
| 자작나무 | Birch |
| 몽글포자나무 | Puffspore |
| 둥둥포자나무 | Driftspore |
| 포자솜나무 | Cottonspore |
| 별뿌리나무 | Starroot |
| 달무리나무 | Moonhalo |
| 은하결나무 | Nebulavein |
| 잿불나무 | Emberwood |
| 용암수액나무 | Lavasap |
| 흑요나무 | Obsidian |

## 플레이어 능력치

| 한국어 | 고정 영문 | 참고 |
|---|---|---|
| 피로도 | stamina | 플레이어 자원에는 `fatigue` 사용 금지 |
| 지구력 | Endurance | 능력명. 자원명과 구분 |
| 피로도 소모량 | stamina drain |  |
| 피로도 최대치 | maximum stamina | 능력명은 `Stamina Capacity` |
| 피로도 회복 | restore stamina | `recover stamina`도 문법상 가능하지만 설명문은 `restore`로 통일 |
| 이동속도 | movement speed | 항상 두 단어로 표기 |
| 속도의 원천 | Burst of Speed | 능력명. 벌목 후 이동속도 증가 효과 |
| 체력의 원천 | Second Wind | 능력명. 이 능력에서 `체력`은 stamina를 의미함 |
| 효율적인 이동 | Efficient Movement | 능력명 |
| 휴식 | Rest | 능력명 |

## 도끼 및 전투

| 한국어 | 고정 영문 |
|---|---|
| 도끼 | axe |
| 도끼 데미지 | axe damage |
| 도끼 내구도 | axe durability |
| 도끼 공격 속도 | axe attack speed |
| 도끼 공격 범위 | axe attack range |
| 도끼 치명타 확률 | axe critical chance |
| 도끼 치명타 데미지 | axe critical damage |
| 강철 도끼 | Steel Axe |
| 도끼 다중 공격 | Multi-Tree Strike |
| 약점 타격 | Weak Point Strike |
| 벌목 리듬 | Logging Rhythm |
| 치명타 | critical hit |
| 치명타 확률 | critical chance |
| 치명타 데미지 | critical damage |
| 공격 속도 버프 | attack speed buff |
| 쿨타임 | cooldown |

## 특수 능력 및 효과

| 한국어 | 고정 영문 | 참고 |
|---|---|---|
| 충격파 | Shockwave | 한 단어로 표기 |
| 충격파 데미지 | Shockwave Damage |  |
| 충격파 범위 | Shockwave Range |  |
| 충격파 파동 강화 | Shockwave Amplification |  |
| 충격파 치명타 적용 | Shockwave Criticals |  |
| 충격파 경지 | Shockwave Mastery |  |
| 부메랑 | Boomerang | 고유 능력으로 취급 |
| 부메랑 데미지 | Boomerang Damage |  |
| 부메랑 범위 | Boomerang Range | 효과 범위 |
| 부메랑 비행거리 | Boomerang Flight Distance | `Range`와 구분 |
| 부메랑 공격 속도 | Boomerang Attack Speed |  |
| 부메랑 쿨타임 감소 | Boomerang Cooldown Reduction |  |
| 포자막 | Spore Shield | `Spore Barrier` 사용 금지 |
| 포자막 회복 | Spore Shield regeneration |  |
| 포자막 회복 억제 | Spore Shield Regen Reduction | 능력명 |
| 포자 절단 | Spore Shield Breaker | 능력명 |
| 포자 관통력 | Spore Shield Penetration |  |
| 포자막 폭발 | Spore Burst | 고유 효과명 |
| 포자막 폭발 증폭 | Spore Burst Damage | 능력명 |
| 포자막 폭발 범위 | Spore Burst Range |  |
| 별 표식 / 별표식 | Star Mark | 항상 띄어 씀 |
| 별자리 | constellation | 일반 명사일 때 소문자 |
| 별자리 발현 | Constellation Strike | 별자리 완성 시 발생하는 공격 |
| 별길 걸음 | Starpath Stride | 능력명 |
| 별표식 베기 | Star Mark Slash | 능력명 |

## 기타 능력명

| 한국어 | 고정 영문 |
|---|---|
| 아낌없이 주는 나무 | The Giving Tree |
| 나무 영양제 | Tree Fertilizer |
| 숙련된 손놀림 | Skilled Hands |
| 전력 공급 | Power Supply |
| 원목 운반 상자 확장 | Cargo Crate Expansion |
| 제재소 원목 보관함 확장 | Sawmill Log Bin Expansion |
| 인벤토리 확장 | Inventory Expansion |
| 원목 수납량 증가 | Log Stack Size |
| 피로도 최대치 증가 | Stamina Capacity |

## 폐기된 기존 번역

- `WideGreen Forest`, `Fluffy Spore Forest`
- `transport container`, `Transport Crate`
- `sawmill log storage`
- `Spore Shield Explosion`
- `Constellation Manifestation`
- 위 표현은 더 이상 신규 번역에 사용하지 않는다.
