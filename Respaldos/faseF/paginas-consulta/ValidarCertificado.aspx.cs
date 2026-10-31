using System;
using System.Collections.Generic;
using System.Data;
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
    public partial class ValidarCertificado : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnValidar_Click(object sender, EventArgs e)
        {
            
            BComprobante comp = new BComprobante();
            lblMensaje.Text = comp.ObtenerCertificado();
            //client.ObtenerEstadoProceso();
        }

        protected void btnBaan_Click(object sender, EventArgs e)
        {
            try
            {
                string _query = "select t$ptov$c as codigo, t$ptov$c || ' - ' || t$dsca$c as descripcion from ttcmcs903700 where t$esta = '1' order by t$ptov$c";
                DataTable dtPuntoVenta = FabricaSingleton<DComprobante>.Crear().ObtenerTabla(_query);
                string strCadena = "";
                foreach (DataRow drFila in dtPuntoVenta.Rows) 
                {
                    strCadena += drFila["codigo"].ToString();
                }
                lblMensaje.Text = strCadena;
            }
            catch (Exception ex)
            {
                lblMensaje.Text = ex.ToString();
            }
        }
    }
}