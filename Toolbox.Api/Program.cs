using DnsClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ILookupClient>(new LookupClient());

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/dns/{domain}", async (string domain, ILookupClient dns) =>
{
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

    return Results.Ok(new
    {
        domain,
        mx = new { status = mxRecords.Count > 0 ? "pass" : "fail", records = mxRecords },
        spf = new
        {
            status = spfRecords.Count == 1 ? "pass" : "fail",
            record = spfRecords.FirstOrDefault()
        }
    });
});

app.Run();

