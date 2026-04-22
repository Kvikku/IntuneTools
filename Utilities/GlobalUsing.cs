// This file is used to define global usings for the project.
//
// Standard BCL namespaces (System, System.Collections.Generic, System.IO,
// System.Linq, System.Net.Http, System.Threading, System.Threading.Tasks)
// are pulled in automatically by <ImplicitUsings>enable</ImplicitUsings>
// in IntuneTools.csproj, so they are intentionally not listed here.

// Project-wide namespaces that are referenced from most files.
global using IntuneTools.Utilities;

// Microsoft Graph (Beta SDK is the primary surface used by this app).
global using Microsoft.Graph.Beta;
global using Microsoft.Graph.Beta.Models;

// Static helpers that are conceptually "ambient" across the app.
global using static IntuneTools.Graph.DestinationUserAuthentication;
global using static IntuneTools.Graph.SourceUserAuthentication;
global using static IntuneTools.Utilities.HelperClass;
global using static IntuneTools.Utilities.TimeSaved;
global using static IntuneTools.Utilities.Variables;
global using static IntuneTools.Utilities.CustomContentInfo;
