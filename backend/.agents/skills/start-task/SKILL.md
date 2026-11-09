---
name: start-task
description: RideMate 백엔드에서 작업을 시작할 때 쓴다. 기능·버그 작업이면 PM vault 작업 카드를 찾거나 만들어 wip로 바꾸고 카드 ID가 들어간 브랜치를 만든다. 설정·문서 작업이면 카드 없이 브랜치만 만든다. "작업 시작", "카드 만들어", "브랜치 따줘"에 쓴다.
---

# start-task

```
VAULT=/Users/stecdev/Desktop/obsidian-workspace/personal_dev
TASK=$VAULT/.claude/scripts/task.sh
```

변수는 셸 호출마다 다시 정의한다. 스크립트는 `bash "$TASK" <명령>` 형태로 부른다. `TASK`에 `bash`까지 넣어 `$TASK ls`로 부르면 zsh에서 실패한다.

## 하지 말 것

- 커밋 안 된 변경이 있으면 진행하지 않는다. 멈추고 사용자에게 알린다.
- 이미 쓰인 카드 번호를 재사용하지 않는다.
- 맞는 카드 후보가 여럿이거나 애매하면 고르지 않고 사용자에게 묻는다.
- 설정·문서 작업에 카드를 만들지 않는다.
- vault에 쓸 수 없으면(샌드박스 권한 등) 카드 없이 진행하지 않는다. 멈추고 사용자에게 알린다.

## 이름

| 작업 | 카드 | 브랜치 |
|---|---|---|
| 기능 | `T0x` | `feat/T0x-<영문-kebab-설명>` |
| 버그 | `T0x` | `fix/T0x-<영문-kebab-설명>` |
| 설정 | 없음 | `chore/<영문-kebab-설명>` |
| 문서 | 없음 | `docs/<영문-kebab-설명>` |

새 카드 ID는 `ls $VAULT/wiki/projects/ride-mate/tasks/`에서 가장 큰 `T` 번호 + 1이다. 두 자리로 쓰고 첫 카드는 `T01`이다.

## 절차

1. `git status --short`로 커밋 안 된 변경이 있는지 본다.
2. `main`을 최신으로 맞춘다: `git switch main && git pull --ff-only`.
3. 설정·문서 작업이면 표의 이름으로 `git switch -c`만 하고 6번으로 간다.
4. 기능·버그면 카드를 찾는다: `bash "$TASK" ls --project ride-mate --status todo`. 사용자가 말한 작업과 맞는 카드가 하나면 그 ID를 쓰고, 없으면 새로 만든다.
   - `bash "$TASK" new T0x "<한국어 제목>" --project ride-mate --section code`
5. 카드를 진행 중으로 바꾸고 브랜치를 만든다.
   - `bash "$TASK" wip T0x --project ride-mate --section code`
   - 기능: `git switch -c feat/T0x-<영문-kebab-설명>`
   - 버그: `git switch -c fix/T0x-<영문-kebab-설명>`
6. 사용자에게 카드 ID와 브랜치 이름을 알린다. 카드가 있으면 "PR을 머지한 뒤 `bash "$TASK" done T0x --project ride-mate`로 닫는다"고 함께 알린다.

## 완료 조건

- [ ] `git branch --show-current`가 표의 이름 형식인 새 브랜치이고, `main`의 최신 커밋에서 갈라졌다.
- [ ] 기능·버그 작업이면 카드가 있고 상태가 wip다(`bash "$TASK" ls --project ride-mate --status wip`에 보인다).
- [ ] 사용자에게 카드 ID와 브랜치 이름을 알렸다.
