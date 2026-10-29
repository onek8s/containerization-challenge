<%@ Page Title="Suspended instances" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Instances.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Suspended instances</h2>
    <div class="filters form-inline">
        <asp:DropDownList ID="ApplicationFilter" runat="server" CssClass="form-control" AppendDataBoundItems="true">
            <asp:ListItem Text="All applications" Value="" />
            <asp:ListItem Text="VanArsdel.B2B.Customers" />
            <asp:ListItem Text="VanArsdel.B2B.Suppliers" />
            <asp:ListItem Text="VanArsdel.B2B.Common" />
        </asp:DropDownList>
        <asp:DropDownList ID="KindFilter" runat="server" CssClass="form-control">
            <asp:ListItem Text="All kinds" Value="" />
            <asp:ListItem Text="Orchestration" />
            <asp:ListItem Text="SendPort" Value="SendPort" />
            <asp:ListItem Text="ReceiveLocation" Value="ReceiveLocation" />
            <asp:ListItem Text="RoutingFailure" Value="RoutingFailure" />
        </asp:DropDownList>
        <asp:TextBox ID="ServiceFilter" runat="server" CssClass="form-control" placeholder="Service contains" />
        <asp:TextBox ID="ErrorFilter" runat="server" CssClass="form-control" placeholder="Error contains" />
        <asp:Button ID="SearchButton" runat="server" Text="Search" CssClass="btn btn-default" OnClick="Search_Click" />
    </div>

    <asp:GridView ID="InstanceGrid" runat="server" ItemType="VanArsdel.Integration.OpsPortal.Models.InstanceRow" SelectMethod="GetInstances"
        DataKeyNames="InstanceId" AutoGenerateColumns="false" AllowPaging="true" PageSize="50"
        CssClass="table table-condensed table-striped" GridLines="None" EmptyDataText="No suspended instances match.">
        <Columns>
            <asp:TemplateField ItemStyle-CssClass="select-row">
                <HeaderTemplate><input type="checkbox" id="check-all" /></HeaderTemplate>
                <ItemTemplate><asp:CheckBox ID="Selected" runat="server" /></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Suspended">
                <ItemTemplate><a href="<%#: "Detail?id=" + Item.InstanceId %>"><%#: Item.SuspendedLocal.ToString("dd.MM. HH:mm:ss") %></a></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="Kind" HeaderText="Kind" />
            <asp:BoundField DataField="Service" HeaderText="Service" />
            <asp:BoundField DataField="Application" HeaderText="Application" />
            <asp:BoundField DataField="HostName" HeaderText="Host" />
            <asp:BoundField DataField="Uri" HeaderText="Address" />
            <asp:BoundField DataField="ErrorCode" HeaderText="Code" />
            <asp:BoundField DataField="ErrorShort" HeaderText="Error" ItemStyle-CssClass="error" />
            <asp:CheckBoxField DataField="Resumable" HeaderText="Resumable" />
        </Columns>
    </asp:GridView>

    <asp:PlaceHolder ID="ActionButtons" runat="server">
        <asp:Button ID="ResumeButton" runat="server" Text="Resume selected" CssClass="btn btn-primary" OnClick="Resume_Click" />
        <asp:Button ID="TerminateButton" runat="server" Text="Terminate selected" CssClass="btn btn-danger" OnClick="Terminate_Click"
            data-confirm="Terminate the selected instances? Their messages are discarded unless tracking keeps them." />
        <span class="help-inline">Terminating an instance keeps the message bodies in the tracking database for 30 days.</span>
    </asp:PlaceHolder>
</asp:Content>
