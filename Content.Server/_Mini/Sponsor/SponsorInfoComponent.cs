// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
//уже не актуально но оставим на всякий случай
internal class SponsorInfoComponent
{
    public class SponsorInfo
    {
        public string Uid { get; set; }
        public int DonateLevel { get; set; }
        public SponsorInfo(string uid, int donateLevel)
        {
            Uid = uid;
            DonateLevel = donateLevel;
        }
    }
    public static List<SponsorInfo> listOfSponsors = new List<SponsorInfo>();
}
