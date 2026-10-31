<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ValidarTicket.aspx.cs" Inherits="FacturacionElectronica.ValidarTicket" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Validar ticket</title>
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
                        <div class="titulos_txt">Validación de ticket</div>
                    </div>
                    <div class="margenes_cuerpo">
                        <div class="parrafo"></div>
                        <div  class="caja_formulatios">
                            <table width="100%" border="0" cellpadding="0" cellspacing="0">
                                <tr>
                                    <td>Ticket:</td>
                                    <td>
                                        <asp:TextBox id="txtTicket" runat="server" /> <asp:Label ID="lblTipo" runat="server" Text="XX"></asp:Label> 
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
