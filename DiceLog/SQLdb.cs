//using System;
using Microsoft.Data.Sqlite;


/*
    The SQLdb class contains methods for interacting with the SQLite database
*/
class SQLdb(string dbname)
{
    //The connexion to the database
    private readonly SqliteConnection dbconnection = new("Data Source=" + dbname);

    //Open the connexion to the database
    public void Open(){
        dbconnection.Open();
    }

    //Close the connexion to the database
    public void Close(){
        dbconnection.Close();
    }

    //Issue a parameterised query to the database
    public SqliteDataReader DbQuery(string query, (string, Object?)[] parameters){
        //Create a command object attached to this dbconnexion
        var command = new SqliteCommand(query, this.dbconnection);

        //Add parameters to the command
        foreach (var param in parameters){
            var (name, val) = param;
            command.Parameters.Add(new SqliteParameter(name, val));
        }

        //Execute and return the query
        /*try {
            return command.ExecuteReader();
        } catch(SqliteException e) {
            Console.WriteLine("+ Error in dbQuery.\n| Query:\n" + query + "\n| SQLite exception: " + e.SqliteExtendedErrorCode);
            return null;
        }*/

        return command.ExecuteReader();
    }

    //Issue a parameterised non-query command to the database. Returns the number of rows inserted, updated, or deleted (-1 for SELECT). Returns -2 for an error.
    public int DbCommand(string command, (string, Object?)[] parameters){
        //Create a command object attached to this dbconnexion
        var commandObj = new SqliteCommand(command, this.dbconnection);
        
        //Add parameters to the command
        foreach (var param in parameters){
            var (name, val) = param;
            commandObj.Parameters.Add(new SqliteParameter(name, val));
        }

        //Execute the command and return results
        /*try {
            return commandObj.ExecuteNonQuery();
        } catch(SqliteException e) {
            Console.WriteLine("+ Error in dbCommand.\n| Command:\n" + command + "\n| SQLite exception: " + e.SqliteExtendedErrorCode);
            return -2;
        }*/
        return commandObj.ExecuteNonQuery();
    }

    //Issue a non-parameterised query to the database
    public SqliteDataReader DbQuery(string query){
        return this.DbQuery(query, []);
    }

    //Issue a non-parameterised command to the database
    public int DbCommand(string command){ 
        return this.DbCommand(command, []);
    }
}
