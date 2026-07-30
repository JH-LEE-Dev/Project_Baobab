---
name: Strict Planning Approval First
description: Forces the agent to always provide an implementation plan and wait for explicit approval before taking any execution actions, even for continuous tasks or feedback.
---

# Always Plan First

1. **무조건적인 계획표 작성 및 승인 대기**
   - 무슨 일이 있어도, 어떤 상황에서도 **무조건적으로 계획표(implementation plan)를 먼저 제공**해야 합니다.
   - 이전 작업과 이어지는 연속적인 작업이거나, 아주 사소하고 간단한 코드 수정이더라도 절대 예외는 없습니다.
   - 사용자가 "확인", "응", "작업 진행해" 등 명시적으로 승인하기 전까지는 절대로 소스 코드를 수정하거나, 파일을 생성하거나, 명령어를 실행하지 마세요.

2. **피드백 수신 시 즉시 작업 금지**
   - 사용자가 기존 계획표에 대해 피드백이나 추가 요구사항을 주었을 때, 스스로 판단하여 즉시 코드를 수정해서는 안 됩니다.
   - 피드백 내용에 맞게 계획표를 재작성(업데이트)하여 다시 제공한 뒤, 사용자의 새로운 승인을 기다려야 합니다.
   - 계획표를 제공한 후에는 즉시 도구 호출을 멈추고 대기하십시오.
