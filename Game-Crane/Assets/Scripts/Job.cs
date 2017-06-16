using System;

#if NETFX_CORE
using Windows.System.Threading;
#else
using System.Threading;
#endif

public class Job
{
  private bool m_finished = false;
  private object m_lock = new object();
#if NETFX_CORE
  private Windows.Foundation.IAsyncAction m_asyncAction = null;
#else
  private Thread m_thread = null;
#endif
  private Action m_function;

  private void Run()
  {
    m_function();
    lock (m_lock)
    {
      m_finished = true;
    }
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
    //TODO: write me
#endif
  }

  public void Join()
  {
#if !NETFX_CORE
    m_thread.Join();
#else
    //TODO: write me
#endif
  }

  public void Execute()
  {
#if !NETFX_CORE
    m_thread.Start();
#else
    m_asyncAction = ThreadPool.RunAsync((workItem) => { Run(); });
#endif
  }

  public Job(Action function)
	{
    m_function = function;
#if !NETFX_CORE
    m_thread = new Thread(Run);
#endif
  }

  ~Job()
  {
    Join();
  }
}
