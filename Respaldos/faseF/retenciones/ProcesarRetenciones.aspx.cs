using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Editora.BusinessLogic;
using Editora.BusinessEntities;
using Editora.BusinessLogic.oServicioComprobante;
using Editora.DataAccess;

namespace FacturacionElectronica
{
    public partial class ProcesarRetenciones : System.Web.UI.Page
    {
        List<EComprobanteRetencion> ListaRetenciones;
        List<ECabeceraReversion> ListaReversiones;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                SetControls();
        }

        public void SetMesActual()
        {
            IFormatProvider cultura = new System.Globalization.CultureInfo("es-PE");
            string FechaNew = string.Empty; string mes = (DateTime.Now.Month).ToString("0#");
            DateTime dFecha;
            if (Convert.ToInt32(mes) < 12)
            {
                FechaNew = "01/" + (DateTime.Now.Month + 1).ToString("0#") + "/" + Convert.ToString(DateTime.Now.Year);
                dFecha = Convert.ToDateTime(FechaNew, cultura);
            }
            else
            {
                FechaNew = "01/" + "01/" + Convert.ToString(DateTime.Now.Year + 1);
                dFecha = Convert.ToDateTime(FechaNew, cultura);
            }

            DateTime dFechaInicio = dFecha.AddMonths(-1);
            DateTime dFechaFin = dFecha.AddDays(-1);

            txtFechaInicio.Text = dFechaInicio.ToString("dd/MM/yyyy");
            txtFechaFin.Text = dFechaFin.ToString("dd/MM/yyyy");

            txtFechaReversion.Text = dFechaInicio.ToString("dd/MM/yyyy");
        }

        private void SetControls()
        {
            //wucFecha1.SetMesActual();
            //wucFechaReversion.SetMesActual();
            SetMesActual();
            string strCompania = AppStatic.CompaniaRetenciones();
            ddlbCompania.SelectedValue = strCompania;
            //litCompania.Text = "Compañía : " + ConfigurationManager.AppSettings["CompaniaRetencion"];
        }

        protected void btnConsultar_Click(object sender, EventArgs e)
        {
            lblCant.Text = "";

            lstComprobantes.Visible = true;
            lstReversiones.Visible = false;

            try
            {
                BRetencion retencion = new BRetencion();

                //if (!wucFecha1.DateFechaInicio.HasValue && !wucFecha1.DateFechaFin.HasValue) return;

                //DateTime fechainicio = wucFecha1.DateFechaInicio.Value;
                //DateTime fechafin = wucFecha1.DateFechaFin.Value;

                DateTime fechainicio = DateTime.Parse(txtFechaInicio.Text);
                DateTime fechafin = DateTime.Parse(txtFechaFin.Text);

                String strEstado = rbEstado.SelectedValue;

                ListaRetenciones = retencion.ObtenerRetenciones(fechainicio, fechafin, strEstado);

                lblCant.Text = "Se encontraron " + ListaRetenciones.Count.ToString() + " registros";

                ViewState["Lista"] = ListaRetenciones;

                CargarData(ListaRetenciones);
            }
            catch (Exception ex)
            {
                lblCant.Text = ex.Message;
            }
        }

        private void CargarData(List<EComprobanteRetencion> listaRetenciones)
        {
            gvComprobanteRetencion.DataSource = listaRetenciones;
            gvComprobanteRetencion.DataBind();
        }

        private void CargarDataReversiones(List<ECabeceraReversion> listaReversiones)
        {
            gvReversiones.DataSource = listaReversiones;
            gvReversiones.DataBind();
        }

        protected void btnProcesar_Click(object sender, EventArgs e)
        {
            lblCant.Text = "";
            lstComprobantes.Visible = true;

            try
            {
                BRetencion comprobante = new BRetencion();

                if (ViewState["Lista"] == null)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }

                ListaRetenciones = (List<EComprobanteRetencion>)ViewState["Lista"];

                if (ListaRetenciones.Count() == 0)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }

                int cant = 0;

                foreach (GridViewRow dtgItem in gvComprobanteRetencion.Rows)
                {
                    CheckBox Sel = ((CheckBox)gvComprobanteRetencion.Rows[dtgItem.RowIndex].FindControl("chkAccept"));

                    if (Sel.Checked)
                    {
                        int id = Convert.ToInt32(gvComprobanteRetencion.DataKeys[dtgItem.RowIndex].Values[0]);

                        EComprobanteRetencion comp = ListaRetenciones.Find(x => x.Id == id);
                        comp.Sel = true;
                    }
                }

                cant = ListaRetenciones.FindAll(x => x.Sel == true).Count();

                if (cant == 0)
                {
                    lblCant.Text = "Debe seleccionar al menos un registro";
                    return;
                }

                ListaRetenciones = comprobante.ProcesarRetenciones(ListaRetenciones);

                lblCantRegAcep.Visible = true;
                lblCantRegRec.Visible = true;

                lblCantRegAcep.Text = ListaRetenciones.FindAll(x => x.CDR == "0" && x.Sel == true).Count().ToString() + " Registros Aceptados";
                lblCantRegRec.Text = ListaRetenciones.FindAll(x => x.CDR != "0" && x.Sel == true).Count().ToString() + " Registros Rechazados";

                CargarData(ListaRetenciones);
            }
            catch (Exception ex)
            {
                lblCant.Text = ex.Message;
                lblCantRegAcep.Visible = false;
                lblCantRegAcep.Visible = false;
            }
        }

        protected void GoPage(object sender, System.EventArgs e)
        {
            DropDownList oIraPag = (DropDownList)sender;
            Int32 iNumPag = 0;

            if (Int32.TryParse(oIraPag.Text.Trim(), out iNumPag) && iNumPag > 0 && iNumPag <= gvComprobanteRetencion.PageCount)
            {
                if ((Int32.TryParse(oIraPag.Text.Trim(), out iNumPag) && iNumPag > 0) && iNumPag <= gvComprobanteRetencion.PageCount)
                    gvComprobanteRetencion.PageIndex = iNumPag - 1;
                else
                    gvComprobanteRetencion.PageIndex = 0;
            }

            ListaRetenciones = (List<EComprobanteRetencion>)ViewState["Lista"];

            CargarData(ListaRetenciones);
        }

        protected void gvComprobanteRetencion_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvComprobanteRetencion.PageIndex = e.NewPageIndex;

            ListaRetenciones = (List<EComprobanteRetencion>)ViewState["Lista"];
            CheckBox chk = (CheckBox)gvComprobanteRetencion.HeaderRow.FindControl("chkheader");
            Boolean val = chk.Checked;

            CargarData(ListaRetenciones);

            if (val)
            {
                CheckBox chk1 = (CheckBox)gvComprobanteRetencion.HeaderRow.FindControl("chkheader");
                chk1.Checked = true;
            }
        }
        private void SetCheckLists(bool check)
        {
            ListaRetenciones = (List<EComprobanteRetencion>)ViewState["Lista"];

            foreach (EComprobanteRetencion item in ListaRetenciones)
            {
                item.Sel = check;
            }

            ViewState["Lista"] = ListaRetenciones;
        }
        protected void chkboxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox ChkBoxHeader = (CheckBox)gvComprobanteRetencion.HeaderRow.FindControl("chkheader");
            foreach (GridViewRow row in gvComprobanteRetencion.Rows)
            {
                CheckBox ChkBoxRows = (CheckBox)row.FindControl("chkAccept");
                if (ChkBoxHeader.Checked == true)
                {
                    ChkBoxRows.Checked = true;
                    SetCheckLists(true);
                }
                else
                {
                    ChkBoxRows.Checked = false;
                    SetCheckLists(false);
                }
            }
        }

        protected void gvComprobanteRetencion_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.Pager && !(gvComprobanteRetencion.DataSource == null))
            {
                Label _TotalPags = (Label)e.Row.FindControl("lblTotalNumberOfPages");
                _TotalPags.Text = gvComprobanteRetencion.PageCount.ToString();

                DropDownList list = (DropDownList)e.Row.FindControl("paginasDropDownList");

                for (int i = 1; i <= gvComprobanteRetencion.PageCount; i++)
                {
                    list.Items.Add(i.ToString());
                }
                list.SelectedValue = (gvComprobanteRetencion.PageIndex + 1).ToString();
            }
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                EComprobanteRetencion beComprobanteRetencion = (EComprobanteRetencion)e.Row.DataItem;
                HiddenField hdnCDR = (HiddenField)e.Row.FindControl("hdnCDR");
                LinkButton hypRazonSocial = (LinkButton)e.Row.FindControl("hypRazonSocial");
                string strFecha = beComprobanteRetencion.FechaEmision.Year.ToString() + @"\" + beComprobanteRetencion.FechaEmision.Month.ToString().PadLeft(2, '0') + @"\" + beComprobanteRetencion.FechaEmision.Day.ToString().PadLeft(2, '0');
                //hypRazonSocial.Text = strFecha;
                hdnCDR.Value = strFecha + @"\20100072751-20-" + beComprobanteRetencion.Serie + @"-" + beComprobanteRetencion.Correlativo + ".pdf";
            }
        }

        protected void gvComprobanteRetencion_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DescargarPDF")
            {
                GridViewRow gvr = (GridViewRow)((Control)e.CommandSource).NamingContainer;
                int rowIndex = gvr.RowIndex;
                HiddenField hdnCDR = (HiddenField)gvr.FindControl("hdnCDR");
                string filename = ConfigurationManager.AppSettings["RutaTemporal"].ToString() + hdnCDR.Value;
                //DescargarPDF
                FileInfo fileInfo = new FileInfo(filename);
                if (fileInfo.Exists)
                {
                    Response.Clear();
                    Response.AddHeader("Content-Disposition", "attachment; filename=" + fileInfo.Name);
                    //Response.AddHeader("Content-Disposition", "inline;attachment; filename=" + fileInfo.Name);
                    Response.AddHeader("Content-Length", fileInfo.Length.ToString());
                    Response.ContentType = "application/octet-stream";
                    Response.Flush();
                    Response.TransmitFile(fileInfo.FullName);
                    Response.End();
                }
            }
        }

        protected void btnConsultarReversion_Click(object sender, EventArgs e)
        {
            lstComprobantes.Visible = false;
            lstReversiones.Visible = true;
            //gvComprobanteRetencion.Visible = false;

            lblReversion.Visible = true;

            try
            {
                BRetencion retencion = new BRetencion();

                //if (!wucFechaReversion.DateFechaInicio.HasValue) return;
                //DateTime fecha = wucFechaReversion.DateFechaInicio.Value;
                DateTime fecha = DateTime.Parse(txtFechaReversion.Text);

                DateTime fechainicio = new DateTime(fecha.Year, fecha.Month, fecha.Day, 0, 0, 0);
                //DateTime fechafin = new DateTime(fecha.Year, fecha.Month, fecha.Day, 23, 59, 59);

                ListaReversiones = retencion.ObtenerReversiones(fechainicio);

                lblReversion.Text = "Se encontraron " + ListaReversiones.Count.ToString() + " reversiones";

                gvReversiones.Visible = ListaReversiones.Count > 0;

                ViewState["ListaReversiones"] = ListaReversiones;

                gvReversiones.DataSource = ListaReversiones;
                gvReversiones.DataBind();
            }
            catch (Exception ex)
            {
                lblReversion.Text = ex.Message;
                gvReversiones.Visible = false;
            }
        }

        protected void btnProcesarReversion_Click(object sender, EventArgs e)
        {
            lblCant.Text = "";
            lstReversiones.Visible = true;
            lstComprobantes.Visible = false;
            try
            {
                BRetencion comprobante = new BRetencion();

                if (ViewState["ListaReversiones"] == null)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }

                ListaReversiones = (List<ECabeceraReversion>)ViewState["ListaReversiones"];

                if (ListaReversiones.Count() == 0)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }


                ListaReversiones = comprobante.ProcesarReversiones(ListaReversiones);

                lblCantRegAcep.Visible = true;
                lblCantRegRec.Visible = true;

                lblCantRegAcep.Text = ListaReversiones.FindAll(x => x.CDR == "0" && x.Sel == true).Count().ToString() + " Registros Aceptados";
                lblCantRegRec.Text = ListaReversiones.FindAll(x => x.CDR != "0" && x.Sel == true).Count().ToString() + " Registros Rechazados";

                CargarDataReversiones(ListaReversiones);
            }
            catch (Exception ex)
            {
                lblCant.Text = ex.Message;
                lblCantRegAcep.Visible = false;
                lblCantRegAcep.Visible = false;
            }

        }

        protected void btnGetCDR_Click(object sender, EventArgs e)
        {
            String strRUC = "20100072751";
            btnProcesar.Enabled = false;
            //lblCant.Text = "";
            lblCantRegAcep.Visible = false;
            lblCantRegRec.Visible = false;

            lstComprobantes.Visible = true;
            try
            {
                BComprobante comprobante = new BComprobante();

                if (ViewState["Lista"] == null)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }

                ListaRetenciones = (List<EComprobanteRetencion>)ViewState["Lista"];

                if (ListaRetenciones.Count() == 0)
                {
                    lblCant.Text = "No existe información a procesar";
                    return;
                }

                foreach (GridViewRow dtgItem in gvComprobanteRetencion.Rows)
                {
                    CheckBox Sel = ((CheckBox)gvComprobanteRetencion.Rows[dtgItem.RowIndex].FindControl("chkAccept"));

                    if (Sel.Checked)
                    {
                        int id = Convert.ToInt32(gvComprobanteRetencion.DataKeys[dtgItem.RowIndex].Values[0]);

                        EComprobanteRetencion comp = ListaRetenciones.Find(x => x.Id == id);

                        String serie = comp.Serie;
                        string strFecha = comp.FechaEmision.ToString("yyyyMMdd");
                        int correlativo = (int)comp.Correlativo;
                        String strTipo = "20";

                        comp.Sel = true;

                        EResultadoProceso resultado = comprobante.ValidarCDR(strRUC, strTipo, serie, correlativo, strFecha);

                        comp.CDR = resultado.CDR;
                        if (comp.CDR == "0")
                            comp.Versionestruct = "ACEPTADO";
                        else
                            comp.Versionestruct = "RECHAZADO";

                        resultado.MsgCDR = resultado.MsgCDR.Replace("'", "");
                        comp.MsgCDR = resultado.MsgCDR;

                        //Actualizar Registro
                        comprobante.ActualizarRetencionCDR(comp);
                    }
                }

                CargarData(ListaRetenciones);

            }
            catch (Exception ex)
            {
                //lblCant.Text = ex.Message;
                lblCantRegAcep.Visible = false;
                lblCantRegAcep.Visible = false;
            }
            finally
            {
                btnProcesar.Enabled = true;
            }
        }
    }
}