using Microsoft.Extensions.Hosting;
using Template.Business.Extensions;
using Template.Data.Extensions;
using Template.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddDataInfrastructure();
builder.AddBusinessServices();
builder.AddContactDigest();

var host = builder.Build();
host.Run();
