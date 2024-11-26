using Microsoft.AspNetCore.Mvc;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedMqttServer(ob =>
{
    ob.WithDefaultEndpoint();
    //ob.WithDefaultEndpointPort(5210);
    ob.WithConnectionBacklog(100);
}).AddMqttConnectionHandler().AddConnections();

builder.WebHost.UseKestrel(
        o =>
        {
            o.ListenAnyIP(1883, l => l.UseMqtt());
            o.ListenAnyIP(5001, l => l.UseHttps());
        }
        );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseRouting();

app.MapConnectionHandler<MqttConnectionHandler>("/mqtt",
        httpConnectionDispatcherOptions => httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector = protocolList => protocolList.FirstOrDefault() ?? string.Empty);

app.MapMqtt("/mqtt");

app.UseMqttServer(
    server =>
    {
        server.ValidatingConnectionAsync += ValidateConnection;
        server.ClientConnectedAsync += arg =>
        {
            Task.Run(async () =>
            {
                int counter = 0;
                while (true)
                {
                    await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(new MqttApplicationMessageBuilder().WithTopic("topic").WithPayload("test" + counter).Build())
                    {
                        SenderClientId = "SenderClientId"
                    });
                    counter++;
                    Thread.Sleep(2000);
                }
            });
            return Task.CompletedTask;
        };
        server.InterceptingSubscriptionAsync += async (arg) =>
        {
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(new MqttApplicationMessageBuilder().WithTopic(arg.TopicFilter.Topic).WithPayload("Test").Build())
            {
                SenderClientId = "SenderClientId"
            });
        };
        server.ClientSubscribedTopicAsync += async (arg) =>
        {
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(new MqttApplicationMessageBuilder().WithTopic(arg.TopicFilter.Topic).WithPayload("Test").Build())
            {
                SenderClientId = "SenderClientId"
            });
        };
    }
    );

async Task Server_ClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs arg)
{
    //using (var mqttServer = new MqttServerFactory().CreateMqttServer(new MqttServerOptionsBuilder().WithDefaultEndpoint().Build()))
    //    await mqttServer.InjectApplicationMessage(
    //            new InjectedMqttApplicationMessage(new MqttApplicationMessageBuilder().WithTopic(arg.TopicFilter.Topic).WithPayload("Test").Build())
    //            {
    //                SenderClientId = "SenderClientId"
    //            });
}

Task Server_InterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs arg)
{

    return Task.CompletedTask;
}

app.MapControllers();

//app.

app.Run();


Task OnClientConnected(ClientConnectedEventArgs eventArgs)
{
    Console.WriteLine($"Client '{eventArgs.ClientId}' connected.");
    return Task.CompletedTask;
}


Task ValidateConnection(ValidatingConnectionEventArgs eventArgs)
{
    Console.WriteLine($"Client '{eventArgs.ClientId}' wants to connect. Accepting!");
    return Task.CompletedTask;
}
