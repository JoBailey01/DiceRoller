using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

/*
    The Client class handles opening connexions to the server and communicating with the server's API
*/
class Client
{
    //The current session token
    private string sessionToken = "";

    //The client's port number
    private int clientPort;

    //The server's port number
    private int serverPort;

    //The server's IP address (always based on the configured value)
    private IPAddress serverIP;

    //The client's current TcpClient
    private TcpClient? tcpClient = null;

    //The client's current SslStream
    private SslStream? sslStream = null;

    //Is the client currently connected to the server?
    private bool isConnected = false;

    /*
        Read a string from the SSL stream. Only strings up to 4096 bytes can be fully read. Returns an empty string for a server that has not started.
        Most of this code comes from here: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream.read?view=net-8.0.
    */
    private string ReadStream(){
        //If there is no SSL stream, return ""
        if(this.sslStream==null) return "";

        //Get UTF-8-encoded bytes
        byte[] buffer = new byte[4096];
        int len = this.sslStream.Read(buffer, 0, 4096);
        System.Text.Decoder decoder = System.Text.Encoding.UTF8.GetDecoder();

        //Convert bytes to a string
        char[] chars = new char[decoder.GetCharCount(buffer, 0, len)];
        decoder.GetChars(buffer, 0, len, chars, 0);
        string output = new string(chars);

        return output;
    }

    /*
        Write a string to the SSL stream. The message will be cut off if its length exceeds 4096 bytes.
        This function also flushes the stream.
        Returns true if there is an open SSL stream to which we can write, returns false otherwise.
    */
    private bool WriteStream(string message){
        //Check the SSL stream
        if(this.sslStream==null) return false;

        //Convert string to UTF-8 encoded bytes
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
        this.sslStream.Write(bytes, 0, bytes.Length > 4096 ? 4096 : bytes.Length);
        this.sslStream.Flush();
        return true;
    }

    /*
        Read the saved session token from .token and store it as this.sessionToken.
        Returns the updated value of this.sessionToken.
    */
    public string ReadToken(){
        //Open the token file
        StreamReader sr = File.OpenText(@"./.token");

        //Read the first line in the file
        string? tok = sr.ReadLine();

        //Do nothing for a null token (do not update the local value)
        if(tok==null){
            sr.Close();
            return this.sessionToken;
        }

        //Otherwise, update the local value
        this.sessionToken = tok;
        sr.Close();
        return this.sessionToken;
    }

    /*
        Update the saved session token in .token.
        Returns true if the update succeeded.
    */
    public bool WriteToken(string newToken){
        this.sessionToken = newToken;
        //Open the token file
        File.WriteAllText(@"./.token", this.sessionToken);
        return true;
    }

    /*
        Create a client with the given port number (or the default port number, if an invalid port number is provided).
        Port numbers are valid if they fall within the range [49152, 65535] and do not overlap with the configured server port. This range avoids reserved ports.
        This constructor does not send any messages to the server.
    */
    public Client(int clientPort, int serverPort){
        //Check validity of input client port number or use the configured default port number
        if(clientPort >= 49152 && clientPort <= 65535 && clientPort != BotService.configs["ServerPort"]){
            this.clientPort = clientPort;
        } else {
            this.clientPort = BotService.configs["ClientPort"];
        }

        //Check validity of input server port number or use the configured default port number
        if(serverPort >= 49152 && serverPort <= 65535 && serverPort != this.clientPort){
            this.serverPort = serverPort;
        } else {
            this.serverPort = BotService.configs["ServerPort"];
        }

        //Set up server IP address based on the configured value
        this.serverIP = IPAddress.Parse(BotService.configs["ServerIP"]);
        
        //Read the latest token value
        this.ReadToken();
    }

    /*
        Certificate validation callback function
        Most of this code is from here: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-8.0.
        Some is from here: https://techcommunity.microsoft.com/t5/apps-on-azure-blog/remote-certificate-is-invalid-when-calling-an-external-endpoint/ba-p/3785758?lightbox-message-images-3785758=456831iD18F6D7812C810C4.
    */
    private static bool ValidateServerCert(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors){
        if(chain==null) return false;

        if(sslPolicyErrors == SslPolicyErrors.None) return true;

        //Handle self-signing errors by hard-coding the server's certificate's thumbprint as allowed
        if(sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) return chain.ChainElements[^1].Certificate.Thumbprint == BotService.configs["ServerThumbprint"];

        Console.WriteLine($"Error in certificate validation: {sslPolicyErrors}");

        return false;
    }

    /*
        Establish a new TCP/SSL connexion to the server.
        Returns true if we established a new connexion or had one open. Returns false if we failed to open the connexion.
        Much of this code comes from here: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-8.0.
    */
    public bool OpenClient(){
        //If we already have a connexion, do not open a new one
        if(this.isConnected) return true;

        //Create the TCP/IP socket. If the connexion fails, return false.
        try {
            this.tcpClient = new TcpClient(serverIP.ToString(), serverPort);
        } catch (System.Net.Sockets.SocketException e){
            Console.WriteLine($"Socket Exception: {e.Message}");
            this.isConnected = false;
            return false;
        }

        //Create the SSL stream
        //this.sslStream = new SslStream(this.tcpClient.GetStream());
        this.sslStream = new SslStream(this.tcpClient.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCert));

        //Set the read/write times
        this.sslStream.ReadTimeout = BotService.configs["ClientReadTimeout"];
        this.sslStream.WriteTimeout = BotService.configs["ClientWriteTimeout"];

        //Authenticate the server using the configured common name (CN)
        try {
            this.sslStream.AuthenticateAsClient(BotService.configs["ServerCN"]);
        } catch (AuthenticationException e){
            Console.WriteLine($"Server authentication failed. Exception: {e.Message}");
            if(e.InnerException != null) Console.WriteLine($"Inner exception: {e.InnerException.Message}");
            Console.WriteLine("Connexion to server closed.");

            this.isConnected = false;
            this.tcpClient.Close();
            return false;
        }

        this.isConnected = true;
        return true;
    }

    /*
        Close the TCP/SSL connexion to the server.
        If there is no outstanding connexion, this function does nothing.
    */
    public void CloseClient(){
        if(!this.isConnected) return;
        if(this.tcpClient!= null) this.tcpClient.Close();
        this.isConnected = false;
        return;
    }

    /*
        Any break in this flow results in the connexion being closed.

        Flow of traffic from server to client:
        1. Client establishes connexion.
        2. Client sends TokenMessage.
            2a. If TokenMessage.isLast==true, server closes the connexion.
            2b. Server authenticates token.
            2c. Server pings client with meaningless data.
        3. Client sends DiscordCommand.
            3a. If 2b returned true, then server processes DiscordCommand.
        4. Server sends DiscordResponse.
            4a. If DiscordResponse.isAuthenticated==false (invalid session token from step 2b):
                4a1. Client sends AuthMessage with username and password.
                    4a1a. If credentials are invalid, server sends AuthResponse with AuthResponse.isAuthenticated=false. Return to step 4a1. Stop after 5 failed attempts.
                    4a1b. If credentials are valid, server sends AuthResponse with AuthResponse.isAuthenticated=true.
                        4a1b1. Client returns to step 2 with the new token and resends both the TokenMessage and the DiscordCommand.
            4b. If DiscordResponse.isAuthenticated==true (valid session token from step 2b):
                4b1. If the token's admin ID does not match the previous admin ID of this stream, then server terminates connexion without sending any message.
                4b2. Client processes DiscordResponse.
        5. Return to step 2.
    */

    /*
        Send a DiscordCommand to the server and receive the response.
        This function also handles authentication, if necessary.
        Returns the DiscordResponse from the server.
    */
    public ServerJson.DiscordResponse SendCommand(ServerJson.DiscordCommand comm){
        //If there is no outstanding connexion, return an empty DiscordResponse
        if(!this.isConnected || this.sslStream==null){
            Console.WriteLine("Client is not open. Command aborted.");
            return new ServerJson.DiscordResponse("{}");
        }

        //First, create and send a TokenMessage (with isLast=false)
        var tokenMessage = new ServerJson.TokenMessage(this.sessionToken, false);
        this.WriteStream(tokenMessage.ToJson());

        //Wait for the server's confirmation ping
        this.ReadStream();

        //Then, send the DiscordCommand
        this.WriteStream(comm.ToJson());

        //Read the server's DiscordResponse
        var discordResponse = new ServerJson.DiscordResponse(this.ReadStream());

        //Display DiscordResponse data
        Console.WriteLine($"Discord Response:\n\tisAuthenticated: {discordResponse.isAuthenticated}\n\tcommandResponse: {(discordResponse.commandResponse==null ? "null" : "not null")}");

        //If the server's response is bad, then return a dummy response
        if(!discordResponse.isCorrect || discordResponse.isAuthenticated == null){
            return new ServerJson.DiscordResponse("{\"isAuthenticated\":false,\"commandResponse\":\"Bad server response\"}");
        }

        //If authentication failed, then send an AuthMessage
        if((bool) !discordResponse.isAuthenticated){
            //Extend client write timeout by 60 seconds to allow for user interaction
            this.sslStream.WriteTimeout = BotService.configs["ClientWriteTimeout"] + 60000;

            //Retrieve username from configuration data
            string username = BotService.configs["ClientUsername"];

            //This process may repeat up to 5 times
            int failedAuths = 0;
            while(failedAuths < 6){
                //If we have had too many failed authentication attempts, then the server will close the connexion, so we will as well
                if(failedAuths >= 5){
                    Console.WriteLine("Authentication failed too many times. Closing connexion to server...");
                    this.sslStream.WriteTimeout = BotService.configs["ClientWriteTimeout"];
                    this.CloseClient();
                    return new ServerJson.DiscordResponse("{\"isAuthenticated\":false,\"commandResponse\":\"Authentication failed\"}");
                }

                //Read the password from the console
                Console.WriteLine($"Enter password for user \'{username}\' ({5-failedAuths} attempts remaining):");
                string? rawPass = BotService.ReadConsoleBlank();
                string pass = rawPass != null ? rawPass : ""; //Empty-string fallback if the user enters no password

                //Send the AuthMessage
                var authMessage = new ServerJson.AuthMessage(username, pass);
                this.WriteStream(authMessage.ToJson());

                //Read the server's AuthResponse
                var authResponse = new ServerJson.AuthResponse(this.ReadStream());

                //If the AuthResponse is bad or the password was wrong, then try again
                if(!authResponse.isCorrect || authResponse.isAuthenticated == null || !((bool) authResponse.isAuthenticated) || authResponse.token==null){
                    failedAuths++;
                    Console.WriteLine("Authentication failed. Retrying...");
                    continue;
                }

                //If the AuthResponse has isAuthenticated==true, then save the new token and retry this DiscordCommand
                if((bool) authResponse.isAuthenticated && authResponse.token != null){
                    this.WriteToken(authResponse.token);
                    
                    this.sslStream.WriteTimeout = BotService.configs["ClientWriteTimeout"];

                    Console.WriteLine("Authentication succeeded");

                    //Resend the original DiscordCommand, now with the new token
                    return this.SendCommand(comm);
                }
            }
        }
        //If authentication succeeded, then return the DiscordResponse from the server
        else {
            return discordResponse;
        }

        return new ServerJson.DiscordResponse("{}");
    }

    /*
        Forcibly send a DiscordCommand, opening a connexion to the server if necessary
        Returns the DiscordResponse from the server.
    */
    public ServerJson.DiscordResponse ForceSendCommand(ServerJson.DiscordCommand comm){
        if(!this.isConnected || this.sslStream==null) this.OpenClient();
        return this.SendCommand(comm);
    }

    /*
        Send a terminating packet to the server (a TokenMessage with isLast=true).
        This function performs no authentication.
    */
    public void SendCloseMessage(){
        //If there is no outstanding connexion, do nothing
        if(!this.isConnected || this.sslStream==null) return;

        //Then, create and send a TokenMessage (with isLast=false)
        var tokenMessage = new ServerJson.TokenMessage(this.sessionToken, true);
        this.WriteStream(tokenMessage.ToJson());
    }

}