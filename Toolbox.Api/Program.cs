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
    var result = await dns.QueryAsync(domain, QueryType.MX);

    var records = result.Answers.MxRecords()
        .OrderBy(r => r.Preference)
        .Select(r => r.Exchange.Value.TrimEnd('.'))
        .ToList();

    return Results.Ok(new
    {
        domain,
        mx = new { status = records.Count > 0 ? "pass" : "fail", records }
    });
});

app.Run();

