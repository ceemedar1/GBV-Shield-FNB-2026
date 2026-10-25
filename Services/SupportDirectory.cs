namespace GBV_Shield.Services;

// Public office contacts checked 2026-09-14. No confidential shelter addresses.
// Keep provenance with each record so maintenance does not depend on name matching.
internal sealed record SupportEntry(string Name, SupportCategory Category, string Address,
    string Phone, string Source, double? Latitude = null, double? Longitude = null,
    string CheckedOn = "14 Sep 2026", string ServiceLabel = "");

internal static class SupportDirectory
{
    internal static readonly SupportEntry[] Entries =
    [
        // Gauteng public hospitals confirmed by the City of Ekurhuleni as 24-hour
        // facilities. Coordinates keep these critical results available without
        // waiting for a third-party geocoder.
        new("Tambo Memorial Hospital", SupportCategory.Hospital,
            "Corner Hospital Road and Railway Street, Plantation, Boksburg, 1459", "0118988000",
            "https://www.ekurhuleni.gov.za/press-releases/top-news/essential-services-uninterrupted-by-closure-of-offices-during-festive-holidays-3/",
            -26.218311, 28.244639, "17 Sep 2026"),
        new("Thelle Mogoerane Regional Hospital", SupportCategory.Hospital,
            "12390 Nguza Street, Extension 14, Vosloorus, 1475", "0118917000",
            "https://www.ekurhuleni.gov.za/press-releases/top-news/essential-services-uninterrupted-by-closure-of-offices-during-festive-holidays-3/",
            -26.356526, 28.223834, "17 Sep 2026"),
        new("Bertha Gxowa Hospital", SupportCategory.Hospital,
            "Corner Joubert and Angus Street, Germiston, 1401", "0112787600",
            "https://www.ekurhuleni.gov.za/press-releases/top-news/essential-services-uninterrupted-by-closure-of-offices-during-festive-holidays-3/",
            -26.219920, 28.163580, "17 Sep 2026"),
        new("Far East Rand Hospital", SupportCategory.Hospital,
            "1 Hospital Road, New State Area, Springs, 1560", "0118128300",
            "https://www.ekurhuleni.gov.za/press-releases/top-news/essential-services-uninterrupted-by-closure-of-offices-during-festive-holidays-3/",
            -26.235720, 28.405620, "17 Sep 2026"),
        new("Pholosong Regional Hospital", SupportCategory.Hospital,
            "1067 Ndaba Street, Tsakane, Brakpan, 1550", "0118125000",
            "https://www.ekurhuleni.gov.za/press-releases/top-news/essential-services-uninterrupted-by-closure-of-offices-during-festive-holidays-3/",
            -26.339850, 28.377040, "17 Sep 2026"),
        new("Boksburg North SAPS", SupportCategory.Police, "37 Fourth Street, Boksburg North, Boksburg, 1459", "0119600100", "https://www.saps.gov.za/contacts/stationdetails.php?sid=233&sname=Boksburg+North+%28Gauteng%29", -26.20587, 28.25301),
        new("Boksburg SAPS", SupportCategory.Police, "112 Commissioner Street, Boksburg, 1459", "0104979900", "https://www.saps.gov.za/contacts/stationdetails.php?sid=225&sname=Boksburg+%28Gauteng%29", -26.22025, 28.23787),
        new("Benoni SAPS", SupportCategory.Police, "117 Harpur Avenue, Benoni, 1501", "0761566327", "https://www.saps.gov.za/contacts/stationdetails.php?sid=1080", -26.19655, 28.31337),
        new("Actonville SAPS", SupportCategory.Police, "73 Singh Street, Actonville, Benoni, 1502", "0119646000", "https://www.saps.gov.za/contacts/stationdetails.php?sid=456", -26.21198, 28.29975),
        new("Germiston SAPS", SupportCategory.Police, "10 Long Street, Germiston, 1400", "0118715003", "https://www.saps.gov.za/contacts/stationdetails.php?sid=252", -26.21722, 28.16174),
        new("Primrose SAPS", SupportCategory.Police, "27 Poppy Street, Primrose, Germiston, 1401", "0118420522", "https://www.saps.gov.za/contacts/stationdetails.php?sid=262&sname=Primrose+%28Gauteng%29", -26.18914, 28.15795),
        new("Elsburg SAPS", SupportCategory.Police, "100 Nagington Road, Wadeville, Germiston, 1407", "0113838200", "https://www.saps.gov.za/contacts/stationdetails.php?sid=249&sname=Elsburg+%28Gauteng%29", -26.260441, 28.188860),
        new("Kidz Clinic Boksburg (WMACA)", SupportCategory.NgoNpo, "99 Market Street, Boksburg, 1459", "0118920404", "https://www.wmaca.org", -26.22236, 28.25799, ServiceLabel: "Child protection & abuse support"),
        new("Mercy Haven", SupportCategory.NgoNpo,
            "Plantation, Boksburg — shelter intake by phone; placement address confirmed privately", "0104440109",
            "https://mercyhavenubuntu.co.za/about-us/index.html", -26.2172, 28.2449,
            CheckedOn: "17 Sep 2026", ServiceLabel: "Women's shelter • women and children"),
        new("POWA Vosloorus", SupportCategory.NgoNpo,
            "1620 Ditshego Street, Vosloorus Rehabilitation Centre, Vosloorus", "0119064259",
            "https://www.powa.co.za/contact/", CheckedOn: "17 Sep 2026", ServiceLabel: "Domestic violence & shelter support"),
        new("POWA Katlehong", SupportCategory.NgoNpo,
            "667 Monise Section, Katlehong, 1431", "0118602858", "https://www.powa.co.za/contact/",
            CheckedOn: "17 Sep 2026", ServiceLabel: "Domestic & sexual violence support"),
        new("AMCARE Victim Empowerment Centre", SupportCategory.NgoNpo,
            "15 Heidelberg Road, Newmarket Park, Alberton", "0658117266",
            "https://www.amcare.org.za/victim-empowerment", CheckedOn: "17 Sep 2026",
            ServiceLabel: "Women's shelter • domestic violence & rape support"),
        new("Thelle Mogoerane Thuthuzela Care Centre", SupportCategory.NgoNpo,
            "Thelle Mogoerane Regional Hospital, 12390 Nguza Street, Vosloorus, 1486", "0115900086",
            "https://www.npa.gov.za/sites/default/files/contacts/List%20of%20Thuthuzela%20Care%20Centres%20in%20South%20Africa%2030%20March%202026.pdf",
            -26.356526, 28.223834, "17 Sep 2026", "Rape & sexual assault care centre"),
        new("TEARS Foundation", SupportCategory.NgoNpo,
            "85 Protea Road, Chislehurston, Sandton, 2196 • Free 24/7 USSD: *134*7355#", "",
            "https://tears.co.za/", CheckedOn: "17 Sep 2026", ServiceLabel: "Rape & GBV crisis support • 24/7 national"),
        new("Boksburg Child Welfare", SupportCategory.NgoNpo, "Corner Trichardt Road and Commissioner Street, Boksburg, 1459", "0119179544", "https://www.childwelfaresagauteng.org/team/boksburg/", ServiceLabel: "Child protection & family support"),
        new("Childline Gauteng", SupportCategory.NgoNpo, "Children's Memorial Institute, 13 Joubert Street Extension, Braampark, Johannesburg, 2017", "0116452000", "https://childlinegauteng.co.za/contact-us/"),
        new("Benoni Child Welfare", SupportCategory.NgoNpo, "1 Third Avenue, Northmead, Benoni, 1501", "0118495241", "https://benonichildwelfare.org.za/contact/", CheckedOn: "15 Sep 2026"),
        new("Germiston Child Welfare", SupportCategory.NgoNpo, "28 Kinross Street, Germiston, 1400", "0118253655", "https://www.childwelfaresagauteng.org/team/germiston/", CheckedOn: "15 Sep 2026"),
        new("Child Welfare Kempton Park", SupportCategory.NgoNpo, "19 Maxwell Street, Kempton Park, Gauteng, 1620", "0106344167", "https://childwelfarekemptonpark.org.za/contact-child-welfare/", CheckedOn: "15 Sep 2026"),
        new("Joburg Child Welfare", SupportCategory.NgoNpo, "41 Fox Street, Ferreiras Dorp, Johannesburg, 2048", "", "https://jhbchildwelfare.org.za/get-in-touch/", CheckedOn: "15 Sep 2026"),
        new("Springs Child Welfare", SupportCategory.NgoNpo, "3 12th Street, Springs, 1560", "0118122345", "https://www.childwelfaresagauteng.org/team/springs/", CheckedOn: "15 Sep 2026"),
        new("The Teddy Bear Clinic", SupportCategory.NgoNpo, "Children's Memorial Institute, 2nd floor, 13 Joubert Street Extension, Braampark, Johannesburg", "0114844554", "https://www.cmi.org.za/web/organisations-The-Teddy-Bear-Clinic.asp", CheckedOn: "15 Sep 2026", ServiceLabel: "Child sexual abuse support"),
        new("Childline Katlehong", SupportCategory.NgoNpo, "Katlehong Resource Centre, Sontonga Street, 824 Ramokunopi West, Katlehong", "0119050920", "https://childlinegauteng.co.za/services/community-centres/", CheckedOn: "15 Sep 2026"),
        new("Childline Soweto", SupportCategory.NgoNpo, "Childline/Pfunanani, Chris Hani Baragwanath Hospital, Old Potchefstroom Road, Soweto", "0119388745", "https://childlinegauteng.co.za/services/community-centres/", CheckedOn: "15 Sep 2026"),
        new("Childline Tembisa", SupportCategory.NgoNpo, "2 Qaqa Street, Corner Andrew Maphetu Drive, Tembisa", "0119261282", "https://childlinegauteng.co.za/services/community-centres/", CheckedOn: "15 Sep 2026"),
        new("Frida Hartley Shelter for Women", SupportCategory.NgoNpo, "97 Regent Street, Bellevue, Johannesburg, 2198", "0116486005", "https://www.fridahartley.org/contact", ServiceLabel: "Women's shelter • women and children")
    ];
}
