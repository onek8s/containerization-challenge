<%@ Page Title="Error" Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>Customer Care - Error</title>
    <link href="<%: ResolveUrl("~/Content/site.css") %>" rel="stylesheet" type="text/css" />
</head>
<body>
    <main>
        <h1>Something went wrong</h1>
        <p>The error has been logged. Go back to the <a href="<%: ResolveUrl("~/") %>">dashboard</a> and try again.</p>
        <p class="muted">If the problem continues, contact the service desk and tell them the time: <%: DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") %> on <%: Environment.MachineName %>.</p>
    </main>
</body>
</html>
