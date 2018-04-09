using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

public class StateObject
{
  public Socket clientSocket = null;
  public const int BufferSize = 1024;
  // Receive buffer.
  public byte[] buffer = new byte[BufferSize];
  // Received data string.
  public StringBuilder sb = new StringBuilder();
}

public class AsynchronousSocketListener
{
  public static ManualResetEvent mre = new ManualResetEvent(false);

  public static Timer reportTimer = new Timer(10000);

  public const int Max_Connections = 5;
  public static int connections = 0;

  public static List<String> duplicateNumbers = new List<string>();
  public static List<String> duplicateNumberstoReport = new List<string>();
  public static List<String> uniqueNumberstoReport = new List<string>();
  public static List<String> loggedNumbers = new List<string>();

  public static string filePath = "numbers.log";

  public static void StartListening()
  {
    //Clear/Delete logfile if it exists
    if(File.Exists(filePath)) File.Delete(filePath);

    //start timer to show Report
    reportTimer.Start();
    reportTimer.Elapsed += timer_Elapsed;

    //  Incoming data.
    byte[] bytes = new Byte[1024];

    IPAddress ipAddress = GetLocalIPAddress();
    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 4000);

    // Create socket: Tcp
    Socket listener = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);

    // Bind socket, listen for connections.
    try {
      listener.Bind(localEndPoint);
      listener.Listen(100);

      while (true) {
        mre.Reset();

        // Listen for connections with an async socket.
        if (connections < Max_Connections) {
          listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
        }
        else {
          mre.Set();
        }

        mre.WaitOne();
      }
    }
    catch (Exception e) {
      Console.WriteLine(e.ToString());
    }
  }

  public static void AcceptCallback(IAsyncResult ar)
  {
    connections++;
    mre.Set();

    // Create listener socket that handles client request
    Socket listener = (Socket)ar.AsyncState;
    Socket handler = listener.EndAccept(ar);

    StateObject state = new StateObject();
    state.clientSocket = handler;
    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
  }

  public static void ReadCallback(IAsyncResult ar)
  {
    string content = String.Empty;

    StateObject state = (StateObject)ar.AsyncState;Socket handler = state.clientSocket;
    
    int bytesRead = handler.EndReceive(ar);

    if (bytesRead > 0) {
      
      // Store the data
      state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

      content = state.sb.ToString();

      // Check if input equals "terminate"
      if (content .Equals("terminate" + Environment.NewLine))
      {
        handler.Shutdown(SocketShutdown.Both);
        handler.Close();
        System.Environment.Exit(0);
      }

      // Validate the input
      if (ValidateInput(content, out int number))
      {
        if (!loggedNumbers.Contains(content))
        {
          // Log unique 9-digit number, and add a newline
          loggedNumbers.Add(content);
          uniqueNumberstoReport.Add(content);
          File.AppendAllText(filePath, content);
        }
        else
        {
          // If the number is a duplicate, add it to a list that
          // will report the number of received duplicates
          duplicateNumbers.Add(content);
          duplicateNumberstoReport.Add(content);
        }
        // Handle additional data
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,new AsyncCallback(ReadCallback), state);
      }
      else
      {
        //Invalid input received, close connection
        handler.Shutdown(SocketShutdown.Both);
        handler.Close();
        connections--;
      }
      state.sb.Clear();
    }
  }

  private static bool ValidateInput(string content, out int number)
  {
    number = 0;

    // Valid input must have a newline, then remove newline
    if (!content.EndsWith(Environment.NewLine)) return false;
    content = content.TrimEnd(Environment.NewLine.ToCharArray());

    // Only allow 9-digit number
    if(content.Length != 9) return false;

    bool valid = int.TryParse(content, out int num);

    if (!valid) return false;

    number = num;
    return true;
  }

  public static int Main(String[] args)
  {
    StartListening();
    return 0;
  }

  private static void timer_Elapsed(object sender, ElapsedEventArgs e)
  {
    Console.WriteLine("Received " + uniqueNumberstoReport.Count.ToString() + " unique numbers, " + duplicateNumberstoReport.Count.ToString() + " duplicates. Unique total: " + loggedNumbers.Count.ToString());

    uniqueNumberstoReport.Clear();
    duplicateNumberstoReport.Clear();
  }

  public static IPAddress GetLocalIPAddress()
  {
    IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
    foreach (IPAddress ip in host.AddressList)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return ip;
        }
    }
    throw new Exception("No network adapters with an IPv4 address in the system!");
  }
}