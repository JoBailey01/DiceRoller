/*
    The Auth class stores static methods related to authentication
*/
class Auth
{
    /*
        Helper function to ensure an user exists. If print is true, this function prints an error message to the console.
    */
    private static bool UserExists(SQLdb db, string username, bool print){
        //Ensure user exists
        var tempReader = db.DbQuery(@"SELECT * FROM admins WHERE admin_name = $input_name;", [("$input_name", username)]);
        if(!tempReader.Read()){
            if(print) Console.WriteLine("User \'" + username + "\' does not exist.");
            return false;
        }
        tempReader.Dispose();
        return true;
    }

    /*
        Helper function to ensure an user exists. If print is true, this function prints an error message to the console.
    */
    private static bool UserExists(SQLdb db, int admin_id, bool print){
        //Ensure user exists
        var tempReader = db.DbQuery(@"SELECT * FROM admins WHERE admin_id = $input_id;", [("$input_id", admin_id)]);
        if(!tempReader.Read()){
            if(print) Console.WriteLine("User with ID " + admin_id + " does not exist.");
            return false;
        }
        tempReader.Dispose();
        return true;
    }

    /*
        Helper function to get an admin_id based on an username. Returns -1 on failure.
    */
    public static int GetAdminID(SQLdb db, string username){
        if(!Auth.UserExists(db, username, true)) return -1;

        var tempReader = db.DbQuery(@"SELECT admin_id FROM admins WHERE admin_name = $input_name;", [("$input_name", username)]);
        if(!tempReader.Read()){
            return -1;
        }
        int output = tempReader.GetInt32(0);
        tempReader.Dispose();
        return output;
    }

    /*
        Check a submitted password against the stored password hash. Return true on success.
    */
    public static bool CheckPassword(SQLdb db, string username, string password){
        if(!Auth.UserExists(db, username, true)) return false;

        //Look for the appropriate salt and salted hash based on the input username
        //var saltReader = db.dbQuery("SELECT salthash FROM admins WHERE admin_name = '@input_name';", [("@input_name", username)]);
        var saltReader = db.DbQuery(@"SELECT salt, salthash FROM admins WHERE admin_name = $input_name;", [("$input_name", username)]);
        
        //Ensure that the output has results
        if(!saltReader.Read()){
            Console.WriteLine("User \'" + username + "\' has entry.");
            return false;
        }

        //Get the salted hash and the salt
        var salt = saltReader.GetString(0);
        var saltHash = saltReader.GetString(1);

        saltReader.Dispose();

        //Create the HashAlgorithm using SHA256
        var hashAlg = System.Security.Cryptography.SHA256.Create();

        //Append the new salt to the password and hash the result to get H(password+salt)
        byte[] passSalt = System.Text.Encoding.UTF8.GetBytes(password + salt); //The byte encoding of password+salt
        byte[] hashBytes = hashAlg.ComputeHash(passSalt); //The hash of passSalt

        hashAlg.Dispose();

        //Compare hashBytes to the stored salthash and return the result
        return Convert.FromBase64String(saltHash).SequenceEqual(hashBytes);
    }

    /*
        Generate a random salt value for an existing user and save it. Returns the new salt, or empty string on failure.
    */
    private static string NewSalt(SQLdb db, string username){
        if(!Auth.UserExists(db, username, true)) return "";

        //Get cryptographically secure random 64-character string using the character set of base64
        string rand = System.Security.Cryptography.RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890+/=", 64);

        //Update the user's salt
        int updated = db.DbCommand(@"UPDATE admins SET salt = $new_salt WHERE admin_name = $username;", [("$new_salt", rand), ("$username", username)]);
        if(updated != 1){
            Console.WriteLine($"Updating salt for user \'{username}\' failed (dbCommand: {updated}).");
            return "";
        }

        return rand;
    }

    /*
        Given an user's new password (which must be at least 3 characters long), compute H(password+salt) and save it as salthash. Returns the salthash, or "" on failure. Always generates and saves new salt.
    */
    public static string NewPassword(SQLdb db, string username, string password){
        //Ensure that the password is at least three characters long
        if (password.Length < 3){
            Console.WriteLine($"Password \'{password}\' for user \'{username}\' is too short. Passwords must be at least 3 characters long.");
            return "";
        }

        //Ensure that user exists
        if(!Auth.UserExists(db, username, true)) return "";

        //Generate and save a new salt for the user
        var salt = NewSalt(db, username);

        //Create the HashAlgorithm using SHA256
        var hashAlg = System.Security.Cryptography.SHA256.Create();

        //Append the new salt to the password and hash the result to get H(password+salt)
        byte[] passSalt = System.Text.Encoding.UTF8.GetBytes(password + salt); //The byte encoding of password+salt
        byte[] hashBytes = hashAlg.ComputeHash(passSalt); //The hash of passSalt

        hashAlg.Dispose();

        //Convert the byte array to a base64 string for the SQL field
        string saltHash = Convert.ToBase64String(hashBytes);

        //Sanity check
        //Console.WriteLine(Convert.FromBase64String(saltHash).SequenceEqual(hashBytes));

        //Update the user's salthash
        int updated = db.DbCommand(@"UPDATE admins SET salthash = $new_salthash WHERE admin_name = $username;", [("$new_salthash", saltHash), ("$username", username)]);
        if(updated != 1){
            Console.WriteLine($"Updating salthash for user \'{username}\' failed (dbCommand: {updated}).");
            return "";
        }


        return saltHash;
    }

    /*
        Creates a new user with the given username and password. Returns true on success. Usernames must be unique and be at least 3 characters long.
    */
    public static bool NewUser(SQLdb db, string username, string password){
        //Ensure that the username is at least three characters long
        if (username.Length < 3){
            Console.WriteLine($"New username \'{username}\' is too short. Usernames must be at least 3 characters long.");
            return false;
        }

        //Stop if this username is already in use
        if(Auth.UserExists(db, username, false)){
            Console.WriteLine($"Username {username} is already in use.");
            return false;
        }

        //Generate a new, unique, random, cryptographically secure admin_id. If there is a collision, generate a new one. Repeat until an unique id is generated.
        int new_id;
        while(true){
            new_id = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, Int32.MaxValue);
            var tempReader = db.DbQuery(@"SELECT * FROM admins WHERE admin_id = $new_id;", [("$new_id", new_id)]);
            if(!tempReader.Read()){
                tempReader.Dispose();
                break;
            }
            tempReader.Dispose();
        }

        //INSERT INTO admins VALUES(0, "Dummy", "QIDBYlSxJRerFH=aiUlAZPjGaXdw3kyeHFlxAKBNuSvS19j9HNATYZHjyeG0xoU5", "q87EWrmwh3zoy5ElofpqYe2f7EItgNJWCRnsY1a2kzU=");
        //Create a new user entry
        int output = db.DbCommand("INSERT INTO admins VALUES($new_id, $username, \"placeholder\", \"placeholder\")", [("$new_id", new_id), ("$username", username)]);

        //If this update failed, return false
        if(output != 1){
            Console.WriteLine($"Error in newUser(). dbCommand output: {output}");
            return false;
        }

        //Create the password data for the new user
        Auth.NewPassword(db, username, password);

        return true;
    }

    /*
        Generates and issues a new random token for an admin and saves it to the sessiontokens table. Returns the token, or "" on failure.
    */
    public static string NewToken(SQLdb db, int admin_id){
        if(!Auth.UserExists(db, admin_id, true)) return "";

        //Get a new, unique, cryptographically secure random 32-character string using the character set of base64
        string token;
        while(true){
            token = System.Security.Cryptography.RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890+/=", 32);
            var tempReader = db.DbQuery(@"SELECT * FROM sessiontokens WHERE token = $token;", [("$token", token)]);
            if(!tempReader.Read()){
                tempReader.Dispose();
                break;
            }
            tempReader.Dispose();
        }

        //Create a new sessiontokens entry
        //int output = db.dbCommand("INSERT INTO sessiontokens VALUES($admin_id, $token, strftime('%FT%TZ', 'now'))", [("$admin_id", admin_id), ("$token", token)]);
        int output = db.DbCommand("INSERT INTO sessiontokens VALUES($admin_id, $token, datetime('now'))", [("$admin_id", admin_id), ("$token", token)]);

        //If this update failed, return failure
        if(output != 1){
            Console.WriteLine($"Error in newToken(). dbCommand output: {output}");
            return "";
        }

        return token;
    }

    /*
        Returns the admin id associated with a token and the how many seconds ago the token was issued. Returns (-1, -1) on failure (e.g., if the token does not exist).
        Output format: (<admin_id>, <seconds_passed>)
    */
    public static (int, int) CheckTokenTime(SQLdb db, string token){
        //Find the token and how recently it was issued
        var tokReader = db.DbQuery(@"SELECT admin_id, token, date_issued, UNIXEPOCH('now') - UNIXEPOCH(date_issued) FROM sessiontokens WHERE token = $token;", [("$token", token)]);
        if(!tokReader.Read()){
            return (-1, -1);
        }

        //Admin ID associated with token
        int admin_id = tokReader.GetInt32(0);
        
        //Time difference from now to when the token was issued
        int timediff = tokReader.GetInt32(3);

        tokReader.Dispose();

        //Return the admin ID and time difference
        return (admin_id, timediff);
    }

    /*
        Returns the admin id associated with a token and whether or not the token is still valid based on configured token timeout. Returns (-1, false) on failure (e.g., if the token does not exist)
        Output format: (<admin_id>, <validity of token>)
    */
    public static (int, bool) CheckTokenValidity(SQLdb db, string token){
        var (admin_id, seconds_passed) = CheckTokenTime(db, token);
        return (admin_id, seconds_passed <= LogService.configs["TokenTimeout"] && seconds_passed >= 0);
    }
}
