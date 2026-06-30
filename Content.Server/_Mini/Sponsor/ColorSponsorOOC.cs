using System;
using System.Collections.Generic;
using System.Drawing;

public class SponsorColor
{
    public static string GetColorForNickname(int DonateLvl)
    {
        string color = "#000000";
        switch (DonateLvl)
        {
            case 1:
                color = "#b4e07ed1";
                return color;
                break;
            case 2:
                color = "#bebaba";
                return color;
                break;
            case 3:
                color = "#e0ad47";
                return color;
                break;
            case 4:
                color = "#a86ed7";
                return color;
                break;
            case 5:
                color = "#e78459";
                return color;
                break;
        }
        return color;
    }


    // public static string GetColorForText(int DonateLvl)
    // {
    //     string color = "#aa00ff";
    //     switch (DonateLvl)
    //     {
    //         case 1:
    //             color = "#77d700";
    //             return color;
    //             break;
    //         case 2:
    //             color = "#c0c0c0";
    //             return color;
    //             break;
    //         case 3:
    //             color = "#ffd700";
    //             return color;
    //             break;
    //         case 4:
    //             color = "#8b00ff";
    //             return color;
    //             break;
    //         case 5:
    //             color = "#c0c0c0";
    //             return color;
    //             break;
    //     }
    //     return color;
    // }
}
