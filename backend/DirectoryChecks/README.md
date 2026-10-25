# Nearby Help verification

Run `dotnet run --project backend/DirectoryChecks/DirectoryChecks.csproj` from the app root.
The console checks compile the production search service with platform stubs and a
deterministic HTTP fixture. They cover relevance, missing addresses, duplicates,
distance ordering, radius filtering, and incomplete Overpass responses. Phone
testing is still required for geocoding, permissions, rendering and launch actions.

## Public directory sources (reviewed 2026-09-14)

Police address/coordinate sources are retained on each entry in
`Services/SupportDirectory.cs`. Benoni uses the SAPS community service centre
mobile number because its page lists a landline-outage contact. The initial saved
police directory covers seven stations near Boksburg; live map search supplements
it elsewhere. It is not a complete nationwide police directory.

NGO sources:

- Kidz Clinic address and telephone: WMACA 2020–2021 annual report,
  https://www.wmaca.org/wp-content/uploads/2021/12/AGM-REPORT-Oct-2020-to-Sept-2021.pdf;
  DSD directory https://www.csd.dsd.gov.za/CsdDirectories/Gauteng.pdf.
  Map coordinate cross-check: https://south-africa.worldplaces.me/view-place/61836379-kidz-clinic-boksburg.html.
  WMACA's current site is temporarily unavailable; operating status is not confirmed.
- Boksburg Child Welfare: https://www.childwelfaresagauteng.org/team/boksburg/.
- Childline public office: https://childlinegauteng.co.za/contact-us/ and
  https://www.cmi.org.za/web/organisations-Childline-Gauteng.asp.
- Frida Hartley public contact address: https://www.fridahartley.org/contact.

Records without published coordinates are geocoded by the phone and omitted if
that fails, with a notice. Directions use the published organisation/address
query rather than claiming geocoded coordinates identify the entrance. Distances
are approximate straight-line distances, not travel distances. Reverse-geocoded
map records are explicitly prefixed with “Near”; house numbers are not invented.
No confidential shelter addresses are included. Contact numbers have not been
called; directory presence does not confirm hours, admission or bed availability.
