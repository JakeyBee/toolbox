using DnsClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ILookupClient>(new LookupClient());

builder.Services.AddCors(options =>                    // NEW
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")    // only your site
            .AllowAnyHeader()
            .WithMethods("GET"));                     // only reads
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(); 

app.MapGet("/api/dns/{domain}", async (string domain, ILookupClient dns) =>
{
    // NEW: clean up, then check, before touching DNS
    domain = domain.Trim().TrimEnd('.').ToLowerInvariant();

    if (!IsValidDomain(domain))
    {
        return Results.BadRequest(new { error = "Not a valid domain name." });
    }

    // MX
    var mxResult = await dns.QueryAsync(domain, QueryType.MX);
    var mxRecords = mxResult.Answers.MxRecords()
        .OrderBy(r => r.Preference)
        .Select(r => r.Exchange.Value.TrimEnd('.'))
        .ToList();

    // SPF
    var txtResult = await dns.QueryAsync(domain, QueryType.TXT);
    var spfRecords = txtResult.Answers.TxtRecords()
        .Select(r => string.Join("", r.Text))
        .Where(t => t.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase))
        .ToList();

    // DMARC
    var dmarcResult = await dns.QueryAsync($"_dmarc.{domain}", QueryType.TXT);
    var dmarcRecords = dmarcResult.Answers.TxtRecords()
        .Select(r => string.Join("", r.Text))
        .Where(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase))
        .ToList();

    // DMARC policy
    var dmarcRecord = dmarcRecords.FirstOrDefault();
    var policy = dmarcRecord?
        .Split(';')
        .Select(part => part.Trim())
        .FirstOrDefault(part => part.StartsWith("p="))
        ?.Substring(2);

    return Results.Ok(new
    {
        domain,
        mx = new { status = mxRecords.Count > 0 ? "pass" : "fail", records = mxRecords },
        spf = new
        {
            status = spfRecords.Count == 1 ? "pass" : "fail",
            record = spfRecords.FirstOrDefault()
        },
        dmarc = new
        {
            status = policy switch
            {
                "reject" or "quarantine" => "pass",
                "none" => "warn",
                _ => "fail"
            },
            policy,
            record = dmarcRecord
        }
    });
});

app.Run();


static bool IsValidDomain(string domain)
{
    if (domain.Length > 253) return false;          

    var labels = domain.Split('.');
    if (labels.Length < 2) return false;            

    foreach (var label in labels)
    {
        if (label.Length is 0 or > 63) return false;              // "a..b" or 70 a's fail
        if (label.StartsWith('-') || label.EndsWith('-')) return false;
        if (!label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return false;  // spaces fail
    }

    return true;
}