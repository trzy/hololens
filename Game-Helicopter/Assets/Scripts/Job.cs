using System;

public class Job
{
  private bool m_finished = false;
  private object m_lock = new object();
  private System.Threading.Thread m_thread = null;
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
    m_thread.Abort();
  }

  public void Join()
  {
    m_thread.Join();
  }

  public void Execute()
  {
    m_thread.Start();
  }

  public Job(Action function)
	{
    m_function = function;
    m_thread = new System.Threading.Thread(Run);
	}

  ~Job()
  {
    Join();
  }
}
