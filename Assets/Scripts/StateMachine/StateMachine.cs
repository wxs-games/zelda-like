public interface IState<T>
{
    void OnEnter(T host);
    void OnUpdate(T host);
    void OnExit(T host);
}

public class StateMachine<T>
{
    private IState<T> currentState;
    private readonly T host;

    public StateMachine(T host)
    {
        this.host = host;
    }

    public void ChangeState(IState<T> next)
    {
        currentState?.OnExit(host);
        currentState = next;
        currentState.OnEnter(host);
    }

    public void Tick()
    {
        currentState?.OnUpdate(host);
    }
}
