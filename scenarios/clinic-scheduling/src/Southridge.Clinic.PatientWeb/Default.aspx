<%@ Page Title="Southridge Medical Group" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Welcome to Southridge Medical Group</h1>
    <asp:LoginView ID="HomeView" runat="server">
        <AnonymousTemplate>
            <p>Book a visit online at any of our clinics, see your upcoming appointments and view your bills.</p>
            <p>
                <asp:HyperLink ID="SignInLink" runat="server" NavigateUrl="~/Account/Login.aspx" CssClass="button">Sign in</asp:HyperLink>
                <asp:HyperLink ID="RegisterLink" runat="server" NavigateUrl="~/Account/Register.aspx" CssClass="button">Create an account</asp:HyperLink>
            </p>
        </AnonymousTemplate>
    </asp:LoginView>
    <asp:Panel ID="NextPanel" runat="server" Visible="false" CssClass="next">
        <h2>Your next appointment</h2>
        <p><asp:Literal ID="NextText" runat="server" Mode="Encode" /></p>
        <asp:HyperLink ID="IntakeLink" runat="server">Fill in the pre-visit form</asp:HyperLink>
    </asp:Panel>
    <asp:Panel ID="NoNextPanel" runat="server" Visible="false">
        <p>You have no upcoming appointments.</p>
    </asp:Panel>
    <p><asp:HyperLink ID="BookLink" runat="server" NavigateUrl="~/Book/Default.aspx" CssClass="button">Book a visit</asp:HyperLink></p>
</asp:Content>
