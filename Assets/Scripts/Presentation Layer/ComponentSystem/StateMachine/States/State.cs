
public abstract class State
{
    protected StateMachine stateMachine;
    protected bool bActivated = false;

    public abstract void Enter();
    public abstract void Exit();
    public abstract void Update();

    /// <summary>
    /// 물리 연산을 위한 FixedUpdate 틱 (가속/감속 처리)
    /// </summary>
    public virtual void FixedUpdate() { }

    public void Release() { UnSubscribeEvents(); }

    protected abstract void SubscribeEvents();
    protected abstract void UnSubscribeEvents();
}