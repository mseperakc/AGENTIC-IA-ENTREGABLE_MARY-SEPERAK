<%@ Page Title="Guías" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" UICulture="es" Culture="es-PE" CodeBehind="Guias.aspx.cs" Inherits="FacturacionElectronica.Guias" %>

<asp:Content ID="cHead" ContentPlaceHolderID="head" runat="server">
</asp:Content>

<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="see-titulo">
        <div>
            <h1>Guías</h1>
            <span class="see-subtitulo">compañía <%: FacturacionElectronica.AppStatic.CompaniaComprobantes() %></span>
        </div>
    </div>

    <section class="see-tarjeta" aria-label="Guías">
        <div class="see-pie" style="border-top: none; padding: 24px 16px; font-size: 13px;">
            Esta sección está en preparación. Su funcionalidad se definirá en la siguiente etapa.
        </div>
    </section>
</asp:Content>
