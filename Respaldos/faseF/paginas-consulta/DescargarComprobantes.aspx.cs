using System;
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
    public partial class DescargarComprobantes : System.Web.UI.Page
    {
        

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnConsultar_Click(object sender, EventArgs e)
        {
            lblCant.Text = "";
            btnDescargarCDR.Visible = false;
            hdnArchivoPDF.Value = "";
            hdnArchivoCDR.Value = "";
            try
            {
                BComprobante comprobante = new BComprobante();

                string strTipo = ddlbTipo.SelectedValue;
                string strSerie = txtSerie.Text;
                string strCorrelativo = txtCorrelativo.Text;

                List<EComprobante> ListaComprobantes = comprobante.ObtenerComprobantesPDF(strTipo, strSerie, strCorrelativo);

                if (ListaComprobantes.Count > 0)
                {
                    btnProcesar.Visible = true;

                    EComprobante comp = ListaComprobantes[0];

                    lblCliente.Text = comp.RazonSocial.Trim();
                    lblRUC.Text = comp.RUC;
                    lblDireccion.Text = comp.DireccionCliente.Trim();
                    lblFecha.Text = comp.Fecha.ToShortDateString();
                    //SI CDR ES ACEPTADO, PERMITIR DESCARGA CDR
                    if (comp.CDR == "0")
                    {
                        btnDescargarCDR.Visible = true;
                        string strTexto = comp.MsgCDR;
                        int intInicioRuc = strTexto.IndexOf("20100072751");
                        strTexto = strTexto.Substring(intInicioRuc);
                        int intInicioComa = strTexto.IndexOf(",");
                        strTexto = strTexto.Substring(0, intInicioComa);

                        string strRutaArchivos = AppStatic.RutaArchivos() + 
                            lblFecha.Text.Substring(6, 4) + @"\" + 
                            lblFecha.Text.Substring(3,2) + @"\" +
                            lblFecha.Text.Substring(0, 2) + @"\";

                        hdnArchivoCDR.Value = strRutaArchivos + "R-" + strTexto + ".xml";
                    }
                    Session["lista"] = ListaComprobantes;

                }
                lblCant.Text = "Se encontró " + ListaComprobantes.Count.ToString() + " registro";

            }
            catch (Exception ex)
            {

                lblCant.Text = ex.ToString();
            }
        }

        protected void btnProcesar_Click(object sender, EventArgs e)
        {
            btnProcesar.Enabled = false;
            btnDescargar.Visible = false;
            lblCant.Text = "";
            try
            {
                List<EComprobante> ListaComprobantes = (List<EComprobante>)Session["lista"];
                BComprobante comprobante = new BComprobante();
                hdnArchivoPDF.Value = comprobante.GenerarPDF(ListaComprobantes);
            }
            catch (Exception ex)
            {
                lblCant.Text = ex.Message;
            }
            finally
            {
                btnProcesar.Enabled = true;
                btnDescargar.Visible = true;
            }
        }

        protected void btnDescargar_Click(object sender, EventArgs e)
        {
            //DescargarPDF
            string filename = hdnArchivoPDF.Value;
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

        protected void btnDescargarCDR_Click(object sender, EventArgs e)
        {
            //DescargarPDF
            string filename = hdnArchivoCDR.Value;
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
}