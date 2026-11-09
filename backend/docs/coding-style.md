# 코딩 스타일

- 메서드 접미사로 레이어를 드러낸다.

| 레이어 | 접미사 | 예 |
|---|---|---|
| Service | `…Service()` | `AddPickupService()` |
| Repository | `…Async()` | `AddPickupAsync()` |
| Controller | 없음 (라우트의 액션 이름 그대로) | `AddPickup()` |

- 필드는 `private readonly _camelCase`, 대입은 `this._x = x;` 형태로 쓴다.
- `await` 뒤에 `.ConfigureAwait(false)`를 붙인다.
- 모든 public 메서드에 한글 `/// <summary>`를 단다.
- 인터페이스는 `I` 접두를 붙이고 구현체와 같은 폴더에 둔다.
