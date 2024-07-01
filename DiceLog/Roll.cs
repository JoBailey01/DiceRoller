using System.Security.Cryptography;

/*
    The Roll class contains static classes for rolling dice of arbitrary sizes
*/
class Roll
{
    /*
        Roll <num> dice, each with <sides> sides (i.e., the result of each rikk must fall within the range [1, <sides>]).
        Defaults to rolling 1 die if `num` <= 1. Defaults to rolling a d6 if `sides` < 2.
        RollDice(x, y) = roll xdy
    */
    public static int[] RollDice(int num, int sides){
        //Only roll a positive number of dice
        if(num <= 1) num = 1;

        //Only roll a meaningfully large die
        if(sides < 2) sides = 6;

        //Output array
        int[] output = new int[num];

        //Roll dice with C#'s random number generator. Sadly, this is pseudo-random, although Microsoft assure me that it is cryptographically strong.
        //The random numbers match the expected statistics pretty well.
        for(int i = 0;i < num;i++){
            output[i] = RandomNumberGenerator.GetInt32(1, sides + 1);
        }

        return output;
    }

    /*
        Roll a single die with <sides> sides (i.e., the result must fall within [1, sides])
        Defaults to rolling a d6 if `sides` < 2.
        RollDie(y) = roll 1dy
    */
    public static int RollDie(int sides){
        if(sides < 2) sides = 6;
        return RandomNumberGenerator.GetInt32(1, sides + 1);
    }
}
