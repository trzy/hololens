using System;

#if NETFX_CORE
using Windows.System.Threading;
using Windows.Foundation;
#else
using System.Threading;
#endif

public class Job
{
  private bool m_finished = false;
  private object m_lock = new object();
#if NETFX_CORE
  private IAsyncAction m_workItem = null;
#else
  private Thread m_thread = null;
#endif
  private Action m_OnBackgroundThread;
  private Action m_OnComplete;

  private void Run()
  {
    m_OnBackgroundThread();
    lock (m_lock)
    {
      m_finished = true;
    }
#if !NETFX_CORE
    if (m_OnComplete != null)
      m_OnComplete();
#endif
  }

  public bool Finished()
  {
    lock (m_lock)
    {
      return m_finished;
    }
  }

  public void Abort()
  {
#if !NETFX_CORE
    m_thread.Abort();
#else
    //TODO: not sure if this is correct. Cancel() requires user's delegate to
    //      cooperate and recognize the cancelation request.
    m_workItem.Cancel();
    Join();
#endif
  }

  public void Join()
  {
#if !NETFX_CORE
    m_thread.Join();
#else
    if (m_workItem != null)
      m_workItem.AsTask().Wait();
#endif
  }

  public void Execute()
  {
#if !NETFX_CORE
    m_thread.Start();
#else
    m_workItem = ThreadPool.RunAsync(
      (workItem) => 
      {
        Run(); 
      });
    m_workItem.Completed = new AsyncActionCompletedHandler(
      (IAsyncAction workItem, AsyncStatus asyncStatus) =>
      {
        if (asyncStatus == AsyncStatus.Canceled)
          return;
        if (m_OnComplete != null)
          m_OnComplete();
      });
#endif
  }

  public Job(Action OnBackgroundThread, Action OnComplete = null)
	{
    m_OnBackgroundThread = OnBackgroundThread;
    m_OnComplete = OnComplete;
#if !NETFX_CORE
    m_thread = new Thread(Run);
#endif
  }

  ~Job()
  {
    Join();
  }
}
