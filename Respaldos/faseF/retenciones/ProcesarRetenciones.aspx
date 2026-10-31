<%@ Page Language="C#" AutoEventWireup="true" UICulture="es" Culture="es-PE" CodeBehind="ProcesarRetenciones.aspx.cs" Inherits="FacturacionElectronica.ProcesarRetenciones" %>
<%@ Register Assembly="AjaxControlToolkit" Namespace="AjaxControlToolkit" TagPrefix="ajax" %>

<%--<%@ Register src="Custom/wucRangoFechas.ascx" tagname="wucRangoFechas" tagprefix="uc1" %>--%>
<%--<%@ Register src="Custom/wucFecha.ascx" tagname="wucFecha" tagprefix="uc2" %>--%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <link href="Styles/EstiloGrilla.css" rel="stylesheet" type="text/css" />
    <link href="Styles/estilos.css" rel="stylesheet" type="text/css">
    <link href="Styles/kendo.common.min.css" rel="stylesheet" type="text/css" />
    <link href="Styles/kendo.default.min.css" rel="stylesheet" type="text/css" />
    <title>Retenciones Electrónicas</title>
    <script src="Scripts/jquery.min.js" type="text/javascript"></script>
    <script src="Scripts/kendo.web.min.js" type="text/javascript"></script>

    <script type="text/javascript">
        function SelectAllCheckboxes(spanChk) {
            var IsChecked = spanChk.checked;
            var cbxAll = spanChk;
            var Parent = document.getElementById('GridViewDatos');
            var items = Parent.getElementsByTagName('input');
            for (i = 0; i < items.length; i++) {
                if (items[i].id != cbxAll.id && items[i].type == "checkbox") {
                    items[i].checked = IsChecked;
                }
            }
        }

    </script>
</head>
<body>
    <form id="form1" runat="server">
    <ajax:ToolkitScriptManager ID="ScriptManager1" runat="server"  />
    <div id="contenedor">
        <div id="contenido">
            <div id="cabecera">
                <div class="logo"><a href="#" class="logo_editora">Logo Editora</a></div>
            </div>
            <div class="salto"></div>
        
            <div class="margenes">
                <div id="cuerpo">
                    <div class="titulos">
                        <div class="titulos_txt">Procesar Retenciones OSE</div>
                    </div>
                    <div class="margenes_cuerpo">
                        <div class="parrafo"></div>
                        <div  class="caja_formulatios">
                            <table width="100%" border="0" cellpadding="0" cellspacing="0">
                                <tr>
                                    <td>Compañía:</td>
                                    <td><asp:DropDownList ID="ddlbCompania" runat="server" Width="200px" Enabled="false">
                                        <asp:ListItem Value="720" Text="720"></asp:ListItem>
                                        <asp:ListItem Value="700" Text="700"></asp:ListItem>
                                    </asp:DropDownList></td>
                                </tr>
                                <tr>
                                    <td>
                                        <asp:Label ID="lblConsulta" runat="server" Text="Consultas: "></asp:Label>
                                    </td>
                                    <td>
                                        <asp:TextBox id="txtFechaInicio" runat="server" />
                                        <script>
                                            $("#txtFechaInicio").kendoDatePicker({
                                                format: "dd/MM/yyyy",
                                                culture: "es-PE"
                                            });
                                        </script>
                                        al
                                        <asp:TextBox id="txtFechaFin" runat="server" />
                                        <script>
                                            $("#txtFechaFin").kendoDatePicker({
                                                format: "dd/MM/yyyy",
                                                culture: "es-PE"
                                            });
                                        </script>
                                        <%--<uc1:wucRangoFechas ID="wucFecha1" runat="server" />--%>
                                        <asp:RadioButtonList ID="rbEstado" runat="server" 
                                                RepeatDirection="Horizontal">
                                                <asp:ListItem Value="1" Selected="True">Pendiente/Error</asp:ListItem>
                                                <asp:ListItem Value="0">Procesado</asp:ListItem>
                                            </asp:RadioButtonList>
                                    </td>
                                </tr>
                                <tr>
                                    <td></td>
                                    <td colspan="3">
                                        <table width="100%">
                                            <tr>
                                                <td>
                                                    <table>
                                                    <tr>
                                                        <td><asp:Button ID="btnConsultar" runat="server" Text="Consultar" 
                                                        onclick="btnConsultar_Click" /></td>
                                                        <td><asp:Button ID="btnProcesar" runat="server" Text="Procesar" 
                                                        onclick="btnProcesar_Click" />                                                
                                                        </td>
                                                        <td>
                                                            <asp:Button ID="btnGetCDR" runat="server" Text="ObtenerCDR" Visible="true"
                                                                OnClick="btnGetCDR_Click" />
                                                        </td>
                                                    </tr>
                                                    </table>
                                                </td>                                            
                                            </tr>
                                            <tr>
                                                <td colspan="5">
                                                <asp:Label runat="server" ID="lblCant" ForeColor="Red" Width="450px"></asp:Label>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>                
                                </tr>
                            </table>
                        </div>
                        <div class="parrafo"></div>
                        <div  class="caja_formulatios">
                            <table width="100%" border="0" cellpadding="0" cellspacing="0">
                            <tr>
                                <td>
                                    <table>
                                        <tr>
                                            <td>
                                                <asp:Label ID="lblReversiones" runat="server" Text="Reversión : "></asp:Label>                                    
                                            </td>
                                            <td>
                                                <%--<uc2:wucFecha ID="wucFechaReversion" runat="server" />--%>
                                                <asp:TextBox id="txtFechaReversion" runat="server" />
                                                <script>
                                                    $("#txtFechaReversion").kendoDatePicker({
                                                        format: "dd/MM/yyyy",
                                                        culture: "es-PE"
                                                    });
                                                </script>
                                            </td>
                                            <td>
                                                <asp:Button ID="btnConsultarReversion" runat="server" Text="Consultar" 
                                                    onclick="btnConsultarReversion_Click" />
                                            </td>
                                            <td>
                                                <asp:Button ID="btnProcesarReversion" runat="server" Text="Procesar" 
                                                    onclick="btnProcesarReversion_Click" />
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                            </table>
                        </div>
                        <div class="parrafo"></div>
                        <table id="lstComprobantes" runat="server" width="100%" border="0" cellpadding="0" cellspacing="0" visible="true">
                        <tr>
                            <td colspan="4">
                                <asp:GridView ID="gvComprobanteRetencion" runat="server" CssClass="grid" Caption="Lista de Comprobantes" 
                                    AutoGenerateColumns="False" PageSize="50" AllowPaging="True" DataKeyNames="Id,FechaEmision"
                                        onpageindexchanging="gvComprobanteRetencion_PageIndexChanging" 
                                        onrowdatabound="gvComprobanteRetencion_RowDataBound" 
                                    onrowcommand="gvComprobanteRetencion_RowCommand">
                                    <Columns>
                                        <asp:TemplateField HeaderText="">
                                        <HeaderTemplate><asp:CheckBox ID="chkheader" runat="server" AutoPostBack="true" OnCheckedChanged="chkboxSelectAll_CheckedChanged"/></HeaderTemplate>
                                        <ItemTemplate><asp:CheckBox ID="chkAccept" runat="server" EnableViewState="true" Checked='<%#Convert.ToBoolean(Eval("Sel")) %>' /></ItemTemplate></asp:TemplateField>
                                        <asp:BoundField HeaderText="Fecha" DataField="FechaEmision" 
                                            DataFormatString="{0:dd/MM/yyyy}" ItemStyle-HorizontalAlign="Left" 
                                            ItemStyle-Width="160px" >
<ItemStyle HorizontalAlign="Left" Width="160px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Serie" DataField="Serie" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Correlativo" DataField="Correlativo" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Tipo" DataField="TipoDocumento" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="RUC" DataField="RUC" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="90px" >
<ItemStyle HorizontalAlign="Left" Width="90px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:TemplateField HeaderText="Razon Social">
                                            <ItemTemplate>
                                                <asp:LinkButton ID="hypRazonSocial" runat="server" CommandName="DescargarPDF" Text='<%# Bind("RazonSocial") %>'></asp:LinkButton>
                                                <asp:HiddenField ID="hdnCDR" Value='<%# Bind("CDR") %>' runat="server" />
                                            </ItemTemplate>
                                            <ItemStyle HorizontalAlign="Left" Width="200px" />
                                        </asp:TemplateField>
                                        <asp:BoundField HeaderText="Importe Ret." DataField="ImporteTotalRetenido" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="200px" >
<ItemStyle HorizontalAlign="Left" Width="200px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Importe Pag." DataField="ImporteTotalPagado" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="300px" >
<ItemStyle HorizontalAlign="Left" Width="300px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="CDR" DataField="CDR" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="140px" >
<ItemStyle HorizontalAlign="Left" Width="140px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Mensaje SUNAT" DataField="MsgCDR" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="550px" >
                            
<ItemStyle HorizontalAlign="Left" Width="550px"></ItemStyle>
                                        </asp:BoundField>
                            
                                    </Columns>
                                    <PagerTemplate>
                                        Página 
                                        <asp:DropDownList ID="paginasDropDownList" Font-Size="12px" AutoPostBack="true" OnSelectedIndexChanged="GoPage" runat="server"></asp:DropDownList>
                                        de
                                        <asp:Label ID="lblTotalNumberOfPages" runat="server" />
                                        &nbsp;&nbsp;
                                        <asp:Button ID="Button4" runat="server" CommandName="Page" ToolTip="Prim. Pag"  CommandArgument="First" CssClass="pagfirst" />                    
                                        <asp:Button ID="Button1" runat="server" CommandName="Page" ToolTip="Pág. anterior"  CommandArgument="Prev" CssClass="pagprev" />
                                        <asp:Button ID="Button2" runat="server" CommandName="Page" ToolTip="Sig. página" CommandArgument="Next" CssClass="pagnext" />                    
                                        <asp:Button ID="Button3" runat="server" CommandName="Page" ToolTip="Últ. Pag"  CommandArgument="Last" CssClass="paglast" />
                                    </PagerTemplate>
                                    <PagerStyle CssClass="pagerstyle" />
                                </asp:GridView>
                            </td>
                        </tr>
                        <tr>
                            <td>
                                <asp:Label ID="lblCantRegAcep" runat="server" Visible="false" ForeColor="Red" Width="450px"></asp:Label>
                            </td>
                        </tr>
                        <tr>
                            <td>
                                <asp:Label ID="lblCantRegRec" runat="server" Visible="false" ForeColor="Red" Width="450px"></asp:Label>
                            </td>
                        </tr>
                        </table>
                        <table id="lstReversiones" runat="server" width="100%" border="0" cellpadding="0" cellspacing="0" visible="true">
                            <tr>
                            <td>
                                <asp:GridView ID="gvReversiones" runat="server" CssClass="grid" Caption="Reversiones" 
                                    AutoGenerateColumns="False" PageSize="50" AllowPaging="True" DataKeyNames="Serie,Correlativo">
                                    <Columns>
                                        <asp:BoundField HeaderText="Fecha" DataField="FechaEmision" 
                                            DataFormatString="{0:dd/MM/yyyy}" ItemStyle-HorizontalAlign="Left" 
                                            ItemStyle-Width="160px" >
<ItemStyle HorizontalAlign="Left" Width="160px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Serie" DataField="Serie" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Correlativo" DataField="Correlativo" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Tipo" DataField="TipoDocumento" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="60px" >
<ItemStyle HorizontalAlign="Left" Width="60px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="RUC" DataField="RUC" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="90px" >
<ItemStyle HorizontalAlign="Left" Width="90px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Razon Social" DataField="RazonSocial" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="200px" >
<ItemStyle HorizontalAlign="Left" Width="90px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Motivo" DataField="MotivoReversion" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="200px" >
<ItemStyle HorizontalAlign="Left" Width="200px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="CDR" DataField="CDR" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="140px" >
<ItemStyle HorizontalAlign="Left" Width="140px"></ItemStyle>
                                        </asp:BoundField>
                                        <asp:BoundField HeaderText="Mensaje SUNAT" DataField="MensajeSunat" 
                                            ItemStyle-HorizontalAlign="Left" ItemStyle-Width="550px" >
                            
<ItemStyle HorizontalAlign="Left" Width="550px"></ItemStyle>
                                        </asp:BoundField>
                            
                                    </Columns>
                                    <PagerTemplate>
                                        Página 
                                        <asp:DropDownList ID="paginasDropDownList" Font-Size="12px" AutoPostBack="true" OnSelectedIndexChanged="GoPage" runat="server"></asp:DropDownList>
                                        de
                                        <asp:Label ID="lblTotalNumberOfPages" runat="server" />
                                        &nbsp;&nbsp;
                                        <asp:Button ID="Button4" runat="server" CommandName="Page" ToolTip="Prim. Pag"  CommandArgument="First" CssClass="pagfirst" />                    
                                        <asp:Button ID="Button1" runat="server" CommandName="Page" ToolTip="Pág. anterior"  CommandArgument="Prev" CssClass="pagprev" />
                                        <asp:Button ID="Button2" runat="server" CommandName="Page" ToolTip="Sig. página" CommandArgument="Next" CssClass="pagnext" />                    
                                        <asp:Button ID="Button3" runat="server" CommandName="Page" ToolTip="Últ. Pag"  CommandArgument="Last" CssClass="paglast" />
                                    </PagerTemplate>
                                    <PagerStyle CssClass="pagerstyle" />
                                </asp:GridView>
                            </td>
                            </tr>
                            <tr>
                                <td>
                                    <asp:Label ID="lblReversion" runat="server" Visible="false" ForeColor="Red" Width="450px"></asp:Label>
                                </td>
                            </tr>
                        </table>
                    </div>

                </div>
                <div class="salto"></div>
            </div>

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
