<%@ Page Title="Error" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.ErrorPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Sorry, something went wrong</h1>
    <p>The error has been recorded. Please try again, and call the help desk at extension 4400 if it keeps happening.</p>
    <p><asp:HyperLink ID="BackLink" runat="server" NavigateUrl="~/Default.aspx">Back to the dashboard</asp:HyperLink></p>
</asp:Content>
