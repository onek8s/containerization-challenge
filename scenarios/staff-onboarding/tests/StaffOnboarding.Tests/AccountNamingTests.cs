using System;
using System.Collections.Generic;
using StaffOnboarding.Core.Naming;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Tests;

public class AccountNamingTests
{
    private readonly AccountNaming _naming = new("corp.local", "litware.com");

    private static PersonDetails Person(string first, string last, string? preferred = null) =>
        new() { FirstName = first, LastName = last, PreferredName = preferred };

    [Fact]
    public void Uses_first_dot_last_in_lower_case()
    {
        var name = _naming.Generate(Person("Anna", "Virtanen"), EmploymentType.Permanent, _ => false);
        Assert.Equal("anna.virtanen", name.SamAccountName);
        Assert.Equal("anna.virtanen@corp.local", name.UserPrincipalName);
        Assert.Equal("anna.virtanen@litware.com", name.Mail);
    }

    [Theory]
    [InlineData("Jääskeläinen", "jaaskelainen")]
    [InlineData("Müller-Lüdenscheidt", "mullerludenscheidt")]
    [InlineData("Ørsted", "orsted")]
    [InlineData("Straße", "strasse")]
    [InlineData("O'Brien", "obrien")]
    [InlineData("Łukasz", "lukasz")]
    public void Normalizes_to_ascii(string input, string expected)
    {
        Assert.Equal(expected, AccountNaming.Normalize(input));
    }

    [Fact]
    public void Uses_preferred_and_first_given_name_only()
    {
        var name = _naming.Generate(Person("Johanna Maria", "Lind", "Hanna"), EmploymentType.Permanent, _ => false);
        Assert.Equal("hanna.lind", name.SamAccountName);

        name = _naming.Generate(Person("Johanna Maria", "Lind"), EmploymentType.Permanent, _ => false);
        Assert.Equal("johanna.lind", name.SamAccountName);
    }

    [Fact]
    public void Falls_back_to_initial_then_number()
    {
        var taken = new HashSet<string> { "anna.virtanen", "a.virtanen" };
        var name = _naming.Generate(Person("Anna", "Virtanen"), EmploymentType.Permanent, taken.Contains);
        Assert.Equal("anna.virtanen2", name.SamAccountName);
    }

    [Fact]
    public void Never_exceeds_twenty_characters()
    {
        var person = Person("Maximilian", "Vanhanen-Kristoffersson");
        var taken = new HashSet<string>();
        for (var i = 0; i < 12; i++)
        {
            var name = _naming.Generate(person, EmploymentType.Permanent, taken.Contains);
            Assert.True(name.SamAccountName.Length <= AccountNaming.MaxSamAccountNameLength, name.SamAccountName);
            Assert.True(taken.Add(name.SamAccountName), "duplicate " + name.SamAccountName);
        }
        Assert.Contains("maximilian.vanhanenk", taken);
        Assert.Contains("m.vanhanenkristoffer", taken);
    }

    [Fact]
    public void Contractors_get_external_prefix_and_mail()
    {
        var name = _naming.Generate(Person("Lena", "Berg"), EmploymentType.Contractor, _ => false);
        Assert.Equal("ext-lena.berg", name.SamAccountName);
        Assert.Equal("lena.berg.ext@litware.com", name.Mail);
    }

    [Fact]
    public void Skips_reserved_names_and_rejects_empty_names()
    {
        Assert.Throws<ArgumentException>(() => _naming.Generate(Person("???", "Smith"), EmploymentType.Permanent, _ => false));
    }

    [Fact]
    public void Throws_when_all_candidates_are_taken()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _naming.Generate(Person("Anna", "Virtanen"), EmploymentType.Permanent, _ => true));
    }
}
