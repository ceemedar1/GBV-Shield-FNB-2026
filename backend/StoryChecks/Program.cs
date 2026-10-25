using GBV_Shield.Services;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

Check(StorySafety.Validate("I found support and I am rebuilding my life safely.") is null, "safe story accepted");
Check(StorySafety.Validate("")?.Contains("20") == true, "empty and short story rejected");
Check(StorySafety.Validate(new string('a', 2001))?.Contains("2,000") == true, "long story rejected");
Check(StorySafety.Validate("Please email person@example.org because this is identifying.")?.Contains("email") == true, "email rejected");
Check(StorySafety.Validate("My number is 067 191 5504 and should not be public.")?.Contains("phone") == true, "phone rejected");
Check(StorySafety.Validate("More information is at https://example.org/private")?.Contains("link") == true, "URL rejected");
Check(StorySafety.Validate("In 2024 I found support and began rebuilding safely.") is null, "ordinary year is not mistaken for phone");
Console.WriteLine($"{passed} story safety checks passed.");
