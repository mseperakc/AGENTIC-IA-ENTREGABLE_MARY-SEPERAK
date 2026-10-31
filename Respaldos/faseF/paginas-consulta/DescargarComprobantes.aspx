<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DescargarComprobantes.aspx.cs" Inherits="FacturacionElectronica.DescargarComprobantes" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Descarga de comprobantes electrónicos</title>
    <link href="Styles/EstiloGrilla.css" rel="stylesheet" type="text/css" />
    <link href="Styles/estilos.css" rel="stylesheet" type="text/css">
</head>
<body>
    <form id="form1" runat="server">
    <div id="contenedor">
        <div id="contenido">
            <div id="cabecera">
                <div class="logo"><a href="#" class="logo_editora">Logo Editora</a></div>
            </div>
            <div class="margenes">
                <div id="cuerpo">
                    <div class="titulos">
                        <div class="titulos_txt">Descarga de comprobantes electrónicos</div>
                    </div>
                    <div class="margenes_cuerpo">
                        <div class="parrafo"></div>
                        <div  class="caja_formulatios">
                            <table width="100%" border="0" cellpadding="0" cellspacing="0">
                                <tr>
                                    <td>Serie Correlativo:</td>
                                    <td>
                                        <asp:DropDownList ID="ddlbTipo" runat="server" Width="100px">
                                            <asp:ListItem Value="Factura">Factura</asp:ListItem>
                                            <%--<asp:ListItem Value="Boleta">Boleta</asp:ListItem>
                                            <asp:ListItem Value="Nota de credi">Nota de crédito</asp:ListItem>
                                            <asp:ListItem Value="Nota de debit">Nota de débito</asp:ListItem>--%>
                                        </asp:DropDownList>
                                        <asp:TextBox id="txtSerie" runat="server" />  
                                        <asp:TextBox id="txtCorrelativo" runat="server" />
                                    </td>
                                    <td><asp:Button ID="btnConsultar" runat="server" Text="Consultar" 
                                            onclick="btnConsultar_Click" /></td>
                                </tr>
                                <tr>
                                    <td colspan="3">
                                        <asp:Label runat="server" ID="lblCant" ForeColor="Red" Width="450px"></asp:Label>
                                    </td>
                                </tr>
                            </table>
                            <table width="100%" border="0" cellpadding="0" cellspacing="0">
                                <tr>
                                    <td>Razón Social:</td>
                                    <td><asp:Label ID="lblCliente" runat="server"></asp:Label></td>
                                </tr>
                                <tr>
                                    <td>RUC/DNI:</td>
                                    <td><asp:Label ID="lblRUC" runat="server"></asp:Label></td>
                                </tr>
                                <tr>
                                    <td>Dirección:</td>
                                    <td><asp:Label ID="lblDireccion" runat="server"></asp:Label></td>
                                </tr>
                                <tr>
                                    <td>Fecha:</td>
                                    <td><asp:Label ID="lblFecha" runat="server"></asp:Label></td>
                                </tr>
                                <tr>
                                    <td></td>
                                    <td><asp:Button ID="btnProcesar" runat="server" Text="Regenerar PDF" Visible="false" 
                                            onclick="btnProcesar_Click"></asp:Button><asp:Button ID="btnDescargar" 
                                            runat="server" Text="Descargar PDF" Visible="false" onclick="btnDescargar_Click" />
                                        <asp:Button ID="btnDescargarCDR" runat="server" Text="CDR" Visible="false" 
                                            onclick="btnDescargarCDR_Click"></asp:Button>
                                        <asp:HiddenField ID="hdnArchivoPDF" runat="server" Visible="false" /><asp:HiddenField ID="hdnArchivoCDR" runat="server" Visible="false" /></td>
                                </tr>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
            <div class="salto"></div>
            <div class="footer_left">
                <asp:Literal ID="litCompania" runat="server"></asp:Literal>
            </div>
            <div class="salto"></div>
        </div>
        <div class="salto"></div>
    </div>
    </form>
</body>
</html>
