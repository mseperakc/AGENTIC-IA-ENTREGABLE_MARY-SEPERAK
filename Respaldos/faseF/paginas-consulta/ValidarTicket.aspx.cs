using System;
using System.Collections.Generic;
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
    public partial class ValidarTicket : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (!IsPostBack)
                {
                    if (Request.QueryString["ticket"] != null)
                    {
                        txtTicket.Text = Request.QueryString["ticket"].ToString();
                    }
                    if (Request.QueryString["tipo"] != null)
                    {
                        lblTipo.Text = Request.QueryString["tipo"].ToString();
                        ValidarTicketSunat(txtTicket.Text);
                    }
                }
            }
            catch (Exception ex)
            {
                
                lblCant.Text = ex.ToString();
            }
        }

        protected void btnConsultar_Click(object sender, EventArgs e)
        {
            try
            {
                ValidarTicketSunat(txtTicket.Text);
            }
            catch (Exception ex)
            {
                
                lblCant.Text = ex.ToString();
            }
        }

        protected void ValidarTicketSunat(string strTicket)
        {
            try
            {
                BComprobante comprobante = new BComprobante();
                lblCant.Text = comprobante.ValidarTicket(txtTicket.Text, lblTipo.Text);
            }
            catch (Exception ex)
            {
                
                throw;
            }
        }
    }
}