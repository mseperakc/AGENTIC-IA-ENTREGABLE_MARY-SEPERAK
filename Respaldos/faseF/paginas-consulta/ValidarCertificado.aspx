<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ValidarCertificado.aspx.cs" Inherits="FacturacionElectronica.ValidarCertificado" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
        <asp:Button ID="btnValidar" runat="server" onclick="btnValidar_Click" 
            Text="Validar certificado" />
    
    </div>
        <div>
        <asp:Button ID="btnBaan" runat="server"  
            Text="Validar conexion a BAAN" onclick="btnBaan_Click" />
    
    </div>
    <div>
    <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </div>
    </form>
</body>
</html>
