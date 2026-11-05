using Microsoft.AspNetCore.Builder;
using SigCoreCommon;

Log.Initialize("WebBridge");
var state = new CoreState(new Log());
state.Start();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

StatusEndpoints.Map(app, state);
var actions = new ApiActions(state);
actions.MapRelay(app);
actions.MapAnalogOutput(app);
actions.MapConfiguration(app);
actions.MapPID(app);
DeviceSettingsEndpoints.Map(app, state);
RestartEndpoints.Map(app, state);
BackupRestoreEndpoints.Map(app, state);
var chartRecorder = new ChartRecorder(state);
ChartEndpoints.Map(app, chartRecorder, state);

app.Lifetime.ApplicationStopping.Register(() => {
    chartRecorder.Dispose();
    state.Stop();
});
System.Console.WriteLine("[Bridge] Private data service listening on 127.0.0.1:8080");
app.Run("http://127.0.0.1:8080");
