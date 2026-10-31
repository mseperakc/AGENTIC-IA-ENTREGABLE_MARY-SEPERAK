using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FACTELECT.SERVICE.DL;
using FACTELECT.SERVICE.EL;
using System.Xml;
using FACTELECT.SERVICE.UTIL;
using ProxyOSE;
using System.IO;
using System.Configuration;
using System.Net;
using Ionic.Zip;
using System.Text.RegularExpressions;
using SysSvcmod = System.ServiceModel.Description;
using System.ServiceModel.Description;

namespace FACTELECT.SERVICE.BL
{
    public class BComprobanteOSE
    {
        private string _HuellaFirmante = "";

        public static BComprobanteOSE Instance()
        {
            return new BComprobanteOSE();
        }

        public static void Escribir_Log(String Mensaje)
        {
            string rutaLog = ConfigurationManager.AppSettings["RutaTemporal"].ToString();
            rutaLog = rutaLog + "/Log.txt";

            StreamWriter log;

            if (!File.Exists(rutaLog)) log = new StreamWriter(rutaLog);
            else log = File.AppendText(rutaLog);

            log.WriteLine(DateTime.Now);
            log.WriteLine(Mensaje);
            log.WriteLine();
            log.Close();
        }

        #region Enviar Factura a Sunat

        public String ObtenerFirmaDigital()
        {
            try
            {
                return new FirmaDigital().ObtenerFirma();
            }
            catch (Exception err)
            {
                return err.ToString();
            }
        }

        public EResultado EnviarFacturaOSE(EFactura factura)
        {
            EResultado eresult = new EResultado();

            string strFormatoFechaEmision = factura.FechaEmision.Substring(0, 4) + factura.FechaEmision.Substring(5, 2) + factura.FechaEmision.Substring(8, 2);
            int intFechaEmision = int.Parse(strFormatoFechaEmision);
            string strEnviarFactura = ConfigurationManager.AppSettings["EnviarFactura"];
            factura.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA
            factura.MensajeAgenteRetencion = ConfigurationManager.AppSettings["MENSAJE_AGENTE_RETENCION"]; //ADD MMILLA
            if (intFechaEmision >= 20180401)
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION_2018"]; //ADD MMILLA
            }
            else
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION"]; //ADD MMILLA
            }

            factura.DetraccionCuenta = ConfigurationManager.AppSettings["DETRACCION_CUENTA"]; //ADD MMILLA
            factura.MensajeDetraccionNO = ConfigurationManager.AppSettings["MENSAJE_DETRACCION_NO"]; //ADD MMILLA
            factura.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            factura.TipoDocumentoEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            factura.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            factura.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            factura.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            factura.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            factura.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            factura.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            factura.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            factura.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            factura.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            factura.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();
            //factura.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //factura.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            try
            {
                Boolean exito = true;
                //CONEXION GESPO
                string strCadenaConexionGESPO = ConfigurationManager.ConnectionStrings["ConexionGespo"].ConnectionString;
                string strCero = "0";
                factura.Observaciones = "";
                //GRATUITA - MENSAJE - LEY
                if (factura.ImporteTotalComprobante == 0)
                {
                    //TOTAL DE OPERACIONES GRATUITAS
                    string strSQLMensaje = "select vchDescripcion, a.vchObservaciones from sgp_comprobante a, SGP_DETALLE_TABLA b where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' and a.intTipotablaG = b.numCodigo and b.vchValor = a.intTipoBaseLegalG";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.DescripcionLey = dtMensaje.Rows[0]["vchDescripcion"].ToString();
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }
                else
                {
                    string strSQLMensaje = "select a.vchObservaciones from sgp_comprobante a where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' ";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }

                StringBuilder _xml = ArmarXmlFacturaUBL21(factura);

                //Envio a Sunat
                byte[] xmlsunat = new Util().GenerarXml(_xml);

                //Guardar xml
                String filenameXml = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".xml";
                String filenameRptaXml = "R-" + factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".xml";

                String filenameZip = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".zip";
                String filenameRptaZip = "R-" + factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".zip";

                String filenamePDF = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".pdf";
                String filenamePDF_ = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + "_.pdf";

                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                //SETEAR POR FECHA DE EMISION
                path += @"\" + factura.FechaEmision.Substring(0, 4) + @"\" + factura.FechaEmision.Substring(5, 2) + @"\" + factura.FechaEmision.Substring(8, 2);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                String tmpXml = path + "/" + filenameXml;

                filenameZip = path + "/" + filenameZip;
                filenameRptaZip = path + "/" + filenameRptaZip;
                filenameRptaXml = path + "/" + filenameRptaXml;

                //Guardar Xml
                if (File.Exists(tmpXml))
                    File.Delete(tmpXml);

                new Util().SaveData(xmlsunat, tmpXml);

                //Fimar Xml
                String SignatureValue = "";
                String DigestValue = "";
                new FirmaDigital().FirmarXmlUBL21(tmpXml, "Invoice", ref SignatureValue, ref DigestValue);

                factura.SignatureValue = SignatureValue;
                factura.DigestValue = DigestValue;

                //Comprimir Xml
                if (File.Exists(filenameZip))
                    File.Delete(filenameZip);

                Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

                //Armar PDF
                filenamePDF_ = path + "/" + filenamePDF_;
                filenamePDF = path + "/" + filenamePDF;

                if (File.Exists(filenamePDF))
                    File.Delete(filenamePDF);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                //Generar PDF
                int intNumeroLineasMax = int.Parse(ConfigurationManager.AppSettings["LINEAS_MAX_PAGINA"].ToString());
                int intLineaDetalle = factura.DetalleFactura.Count();
                int intNumPaginas = 1;
                if (intLineaDetalle % intNumeroLineasMax == 0)
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax);
                else
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax) + 1;

                FabricaSingleton<FacturaPDF>.Crear().GenerarPDF(filenamePDF_, factura, intNumPaginas, intNumeroLineasMax);
                itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                try
                {
                    if (strEnviarFactura == "SI")
                    {
                        byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                        ServicePointManager.Expect100Continue = false;
                        //TLS1.2
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                        //OSE
                        ClientCredentials clientCredentials = new ClientCredentials();
                        clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                        clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];
                        
                        IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                        var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                        proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials); 

                        proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                        proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                        proxy.Close();

                        if (File.Exists(filenameRptaZip))
                            File.Delete(filenameRptaZip);

                        if (File.Exists(filenameRptaXml))
                            File.Delete(filenameRptaXml);

                        new Util().SaveData(oSalida, filenameRptaZip);

                        using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                        {
                            zip.ExtractAll(path);
                        }

                        //Leer Xml Respuesta Sunat
                        XmlDocument xmlDocRptaSunat = new XmlDocument();
                        xmlDocRptaSunat.Load(filenameRptaXml);

                        XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                        XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                        XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                        string responsecode = String.Empty, description = String.Empty;

                        foreach (XmlNode nodo in lista)
                        {
                            responsecode = nodo["cbc:ResponseCode"].InnerText;
                            description = nodo["cbc:Description"].InnerText;
                        }

                        String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                        eresult.CDR = responsecode;
                        eresult.EstadoComprobante = EstadoComprobante;
                        eresult.MensajeResultadoSunat = description.Replace("'", "");

                        //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
                    }
                    else
                    {
                        exito = true;
                        eresult.CDR = "0";
                    }
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    String mensajesunat = "";
                    System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                    if (dtmensaje.Rows.Count > 0)
                    {
                        mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                    }

                    eresult.CDR = ex.Code.Name;
                    eresult.EstadoComprobante = "RECHAZADO";

                    if (!String.IsNullOrEmpty(mensajesunat))
                        eresult.MensajeResultadoSunat = mensajesunat.Replace("'", "");
                    else
                        eresult.MensajeResultadoSunat = ex.Message.Replace("'", "");
                    if (ex.Message.Length > 215)
                        eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                    exito = false;
                }
            }
            catch (Exception ex)
            {
                Escribir_Log(ex.Message);
            }

            return eresult;
        }

        public EResultado EnviarFacturaPDF(EFactura factura)
        {
            EResultado eresult = new EResultado();

            string strFormatoFechaEmision = factura.FechaEmision.Substring(0, 4) + factura.FechaEmision.Substring(5, 2) + factura.FechaEmision.Substring(8, 2);
            int intFechaEmision = int.Parse(strFormatoFechaEmision);
            string strEnviarFactura = ConfigurationManager.AppSettings["EnviarFactura"];
            factura.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA
            factura.MensajeAgenteRetencion = ConfigurationManager.AppSettings["MENSAJE_AGENTE_RETENCION"]; //ADD MMILLA
            if (intFechaEmision >= 20180401)
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION_2018"]; //ADD MMILLA
            }
            else
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION"]; //ADD MMILLA
            }

            factura.MensajeDetraccionNO = ConfigurationManager.AppSettings["MENSAJE_DETRACCION_NO"]; //ADD MMILLA
            factura.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            factura.TipoDocumentoEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            factura.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            factura.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            factura.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            factura.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            factura.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            factura.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            factura.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            factura.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            factura.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            factura.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();
            //factura.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //factura.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            try
            {
                Boolean exito = true;
                //CONEXION GESPO
                string strCadenaConexionGESPO = ConfigurationManager.ConnectionStrings["ConexionGespo"].ConnectionString;
                string strCero = "0";
                factura.Observaciones = "";
                //GRATUITA - MENSAJE - LEY
                if (factura.ImporteTotalComprobante == 0)
                {
                    //TOTAL DE OPERACIONES GRATUITAS
                    string strSQLMensaje = "select vchDescripcion, a.vchObservaciones from sgp_comprobante a, SGP_DETALLE_TABLA b where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' and a.intTipotablaG = b.numCodigo and b.vchValor = a.intTipoBaseLegalG";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.DescripcionLey = dtMensaje.Rows[0]["vchDescripcion"].ToString();
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }
                else
                {
                    string strSQLMensaje = "select a.vchObservaciones from sgp_comprobante a where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' ";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }

                String filenamePDF = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".pdf";
                String filenamePDF_ = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + "_.pdf";

                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                //SETEAR POR FECHA DE EMISION
                path += @"\" + factura.FechaEmision.Substring(0, 4) + @"\" + factura.FechaEmision.Substring(5, 2) + @"\" + factura.FechaEmision.Substring(8, 2);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                //Armar PDF
                filenamePDF_ = path + "/" + filenamePDF_;
                filenamePDF = path + "/" + filenamePDF;

                if (File.Exists(filenamePDF))
                    File.Delete(filenamePDF);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                //Generar PDF
                int intNumeroLineasMax = int.Parse(ConfigurationManager.AppSettings["LINEAS_MAX_PAGINA"].ToString());
                int intLineaDetalle = factura.DetalleFactura.Count();
                int intNumPaginas = 1;
                if (intLineaDetalle % intNumeroLineasMax == 0)
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax);
                else
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax) + 1;

                FabricaSingleton<FacturaPDF>.Crear().GenerarPDF(filenamePDF_, factura, intNumPaginas, intNumeroLineasMax);
                itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                try
                {
                    if (strEnviarFactura == "SI")
                    {
                        //byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                        //ServicePointManager.Expect100Continue = false;

                        ////SUNAT
                        //billServiceClient proxy = new billServiceClient("CurrencyServiceSoap");
                        ////OSE
                        ////IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoap");
                        //proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        //byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                        //proxy.Close();

                        //if (File.Exists(filenameRptaZip))
                        //    File.Delete(filenameRptaZip);

                        //if (File.Exists(filenameRptaXml))
                        //    File.Delete(filenameRptaXml);

                        //new Util().SaveData(oSalida, filenameRptaZip);

                        //using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                        //{
                        //    zip.ExtractAll(path);
                        //}

                        ////Leer Xml Respuesta Sunat
                        //XmlDocument xmlDocRptaSunat = new XmlDocument();
                        //xmlDocRptaSunat.Load(filenameRptaXml);

                        //XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                        //XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                        //XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                        //string responsecode = String.Empty, description = String.Empty;

                        //foreach (XmlNode nodo in lista)
                        //{
                        //    responsecode = nodo["cbc:ResponseCode"].InnerText;
                        //    description = nodo["cbc:Description"].InnerText;
                        //}

                        //String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                        //eresult.CDR = responsecode;
                        //eresult.EstadoComprobante = EstadoComprobante;
                        //eresult.MensajeResultadoSunat = description.Replace("'", "");
                        exito = true;
                        eresult.CDR = "9";
                        eresult.EstadoComprobante = "GENERADO";
                        eresult.MensajeResultadoSunat = "No enviado a SUNAT";
                    }
                    else
                    {
                        exito = true;
                        eresult.CDR = "0";
                    }
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    String mensajesunat = "";
                    System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                    if (dtmensaje.Rows.Count > 0)
                    {
                        mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                    }

                    eresult.CDR = ex.Code.Name;
                    eresult.EstadoComprobante = "RECHAZADO";

                    if (!String.IsNullOrEmpty(mensajesunat))
                        eresult.MensajeResultadoSunat = mensajesunat.Replace("'", "");
                    else
                        eresult.MensajeResultadoSunat = ex.Message.Replace("'", "");
                    if (ex.Message.Length > 215)
                        eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                    exito = false;
                }
            }
            catch (Exception ex)
            {
                Escribir_Log(ex.Message);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlFactura(EFactura factura)
        {
            EstructuraXMLFactura estructura = new EstructuraXMLFactura();

            StringBuilder _xml = new StringBuilder();

            String encoding = ConfigurationManager.AppSettings["Encoding"];
            String UBLVersionID = ConfigurationManager.AppSettings["UBLVersionID"];
            String CustomizationID = ConfigurationManager.AppSettings["CustomizationID"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
              "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
              "<ext:UBLExtensions>" +
                "<ext:UBLExtension>" +
                  "<ext:ExtensionContent>" +
                    "<sac:AdditionalInformation>" +
                      "<sac:AdditionalMonetaryTotal>" +
                        "<cbc:ID>1001</cbc:ID>" +
                        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                      "</sac:AdditionalMonetaryTotal>" +
                      "<sac:AdditionalMonetaryTotal>" +
                        "<cbc:ID>1002</cbc:ID>" +
                        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                      "</sac:AdditionalMonetaryTotal>" +

                      (factura.TotalVentaOpExoneradas > 0 ?
                        "<sac:AdditionalMonetaryTotal>" +
                        "<cbc:ID>1003</cbc:ID>" +
                        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                        "</sac:AdditionalMonetaryTotal>" : String.Empty) +


                      (factura.TotalVentaOpGratuitas > 0 ?
                                                    "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1004</cbc:ID>" +

                                                    "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" +
                                                    "<sac:AdditionalProperty>" +
                                                    "<cbc:ID>1002</cbc:ID>" +
                //"<cbc:Value>" + factura.Motivo + " " + factura.DescripcionLey + "</cbc:Value>" +
                                                    "<cbc:Value>" + factura.Motivo + "</cbc:Value>" +
                                                    "</sac:AdditionalProperty>" : String.Empty) +

                      (factura.TotalDescuentos > 0 ?
                        "<sac:AdditionalMonetaryTotal>" +
                        "<cbc:ID>2005</cbc:ID>" +
                        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                        "</sac:AdditionalMonetaryTotal>" : String.Empty) +

                      "<sac:AdditionalProperty>" +
                        "<cbc:ID>1000</cbc:ID>" +
                        "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.MontoEnLetras) + "]]></cbc:Value>" +
                      "</sac:AdditionalProperty>" +
                    "</sac:AdditionalInformation>" +
                  "</ext:ExtensionContent>" +
                "</ext:UBLExtension>" +
                "<ext:UBLExtension>" +
                    "<ext:ExtensionContent>" +
                    "</ext:ExtensionContent>" +
                "</ext:UBLExtension>" +
              "</ext:UBLExtensions>" +
              "<cbc:UBLVersionID>" + UBLVersionID + "</cbc:UBLVersionID>" +
              "<cbc:CustomizationID>" + CustomizationID + "</cbc:CustomizationID>" +
              "<cbc:ID>" + factura.NumeroComprobante + "</cbc:ID>" +
              "<cbc:IssueDate>" + factura.FechaEmision + "</cbc:IssueDate>" +
              "<cbc:InvoiceTypeCode>" + factura.CodigoTipoComprobante + "</cbc:InvoiceTypeCode>" +
              "<cbc:DocumentCurrencyCode>" + factura.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
              "<cac:Signature>" +
                "<cbc:ID>IDSignKG</cbc:ID>" +
                "<cac:SignatoryParty>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID>" + factura.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.NombreComercialEmisor) + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                "</cac:SignatoryParty>" +
                "<cac:DigitalSignatureAttachment>" +
                  "<cac:ExternalReference>" +
                    "<cbc:URI>SignatureSP</cbc:URI>" +
                  "</cac:ExternalReference>" +
                "</cac:DigitalSignatureAttachment>" +
              "</cac:Signature>" +
              "<cac:AccountingSupplierParty>" +
                "<cbc:CustomerAssignedAccountID>" + factura.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                "<cbc:AdditionalAccountID>" + factura.TipoDocumentoEmisor + "</cbc:AdditionalAccountID>" +
                "<cac:Party>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                  "<cac:PostalAddress>" +
                    "<cbc:ID>" + factura.UbigeoEmisor + "</cbc:ID>" +
                    "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionEmisor) + "]]></cbc:StreetName>" +
                //"<cbc:CitySubdivisionName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.UrbanizacionEmisor) + "]]></cbc:CitySubdivisionName>" +
                    "<cbc:CityName>" + factura.DepartamentoEmisor + "</cbc:CityName>" +
                    "<cbc:CountrySubentity>" + factura.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                    "<cbc:District>" + factura.DistritoEmisor + "</cbc:District>" +
                    "<cac:Country>" +
                      "<cbc:IdentificationCode>" + factura.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                    "</cac:Country>" +
                  "</cac:PostalAddress>" +
                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                  "</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingSupplierParty>" +
              "<cac:AccountingCustomerParty>" +
                "<cbc:CustomerAssignedAccountID>" + factura.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" +
                "<cbc:AdditionalAccountID>" + factura.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +
                "<cac:Party>" +
                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                    "<cac:RegistrationAddress>" +
                        "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionAdquirente) + "]]></cbc:StreetName>" +
                    "</cac:RegistrationAddress>" +
                  "</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingCustomerParty>" +
              "<cac:TaxTotal>" +
                "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID>1000</cbc:ID>" +
                      "<cbc:Name>IGV</cbc:Name>" +
                      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" +
              "</cac:TaxTotal>" +

              (factura.OTRTotalComprobante > 0 && factura.TotalVentaOpGratuitas == 0 ?
                "<cac:TaxTotal>" +
                    "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                    "<cac:TaxSubtotal>" +
                        "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                        "<cac:TaxCategory>" +
                            "<cac:TaxScheme>" +
                                "<cbc:ID>9999</cbc:ID>" +
                                "<cbc:Name>OTROS</cbc:Name>" +
                                "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
                            "</cac:TaxScheme>" +
                        "</cac:TaxCategory>" +
                    "</cac:TaxSubtotal>" +
                    "</cac:TaxTotal>" : String.Empty) +

              "<cac:LegalMonetaryTotal>" +
                (factura.TotalDescuentos > 0 ? "<cbc:AllowanceTotalAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:AllowanceTotalAmount>" : String.Empty) +
                (factura.OTRCTotalComprobante > 0 ? "<cbc:ChargeTotalAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRCTotalComprobante.ToString("#0.00") + "</cbc:ChargeTotalAmount>" : String.Empty) +

                (factura.TotalVentaOpGratuitas == 0 && factura.TotalDescuentos == 0 ?

                                    "<cbc:LineExtensionAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.ImporteBrutoComprobante.ToString("#0.00") + "</cbc:LineExtensionAmount>" +




                                    "<cbc:TaxExclusiveAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +

                "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.CodigoTipoMoneda == "USD" ?
                                                                                         factura.ImporteBrutoComprobante.ToString("#0.00") :
                                                                                         factura.ImporteTotalComprobante.ToString("#0.00"))
                                                                                      + "</cbc:PayableAmount>" +
              "</cac:LegalMonetaryTotal>";

            String detalle = "";

            foreach (EDetalleFactura item in factura.DetalleFactura)
            {
                detalle = detalle + "<cac:InvoiceLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:InvoicedQuantity unitCode=\"" + item.UnidadMedida + "\">" + item.Cantidad.ToString("#0.000") + "</cbc:InvoicedQuantity>" +
                            (factura.ImporteTotalComprobante == 0 ?
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:LineExtensionAmount>" :
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            "<cac:PricingReference>" +
                              (factura.ImporteTotalComprobante == 0 ?
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>01</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" +
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>02</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" :
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>") +

                            "</cac:PricingReference>" +
                            "<cac:AllowanceCharge>" +
                              "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                              "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                            "</cac:AllowanceCharge>" +
                            "<cac:TaxTotal>" +
                              (factura.ImporteTotalComprobante == 0 ?
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" :
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") +
                              "<cac:TaxSubtotal>" +
                                (factura.ImporteTotalComprobante == 0 ?
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" :
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") +
                    //"<cbc:Percent>" + factura.ValorIGV.ToString("#0.0") +"</cbc:Percent>" +
                                "<cac:TaxCategory>" +
                                  "<cbc:TaxExemptionReasonCode>" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    "<cbc:ID>1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                            "</cac:Item>" +
                            "<cac:Price>" +
                                (factura.ImporteTotalComprobante == 0 ?
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" :
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.00") + "</cbc:PriceAmount>") +
                              "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                                "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                              "</cac:AllowanceCharge>" +
                            "</cac:Price>" +
                          "</cac:InvoiceLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</Invoice>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        private StringBuilder ArmarXmlFacturaUBL21(EFactura factura)
        {
            //EstructuraXMLFactura estructura = new EstructuraXMLFactura();

            StringBuilder _xml = new StringBuilder();
            Decimal valorIVA = 0.18m;
            String encoding = "UTF-8";
            String UBLVersionID = "2.1";
            String CustomizationID = "2.0";

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
              "<Invoice xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" " +
              "xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" " +
              "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" " +
              "xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" " +
              "xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" " +
              "xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" " +
              //"xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" "+
              "xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" " +
              "xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\">" +
              "<ext:UBLExtensions>" +
                //"<ext:UBLExtension>" +
                //  "<ext:ExtensionContent>" +
                //    "<sac:AdditionalInformation>" +
                //      "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1001</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //      "</sac:AdditionalMonetaryTotal>" +
                //      "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1002</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //      "</sac:AdditionalMonetaryTotal>" +

                //      (factura.TotalVentaOpExoneradas > 0 ?
                //        "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1003</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //        "</sac:AdditionalMonetaryTotal>" : String.Empty) +


                //      (factura.TotalVentaOpGratuitas > 0 ?
                //                                    "<sac:AdditionalMonetaryTotal>" +
                //                                    "<cbc:ID>1004</cbc:ID>" +

                //                                    "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                                    "</sac:AdditionalMonetaryTotal>" +
                //                                    "<sac:AdditionalProperty>" +
                //                                    "<cbc:ID>1002</cbc:ID>" +
                ////"<cbc:Value>" + factura.Motivo + " " + factura.DescripcionLey + "</cbc:Value>" +
                //                                    "<cbc:Value>" + factura.Motivo + "</cbc:Value>" +
                //                                    "</sac:AdditionalProperty>" : String.Empty) +

                //      (factura.TotalDescuentos > 0 ?
                //        "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>2005</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                //        "</sac:AdditionalMonetaryTotal>" : String.Empty) +

                //      "<sac:AdditionalProperty>" +
                //        "<cbc:ID>1000</cbc:ID>" +
                //        "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.MontoEnLetras) + "]]></cbc:Value>" +
                //      "</sac:AdditionalProperty>" +
                //    "</sac:AdditionalInformation>" +
                //  "</ext:ExtensionContent>" +
                //"</ext:UBLExtension>" +
                "<ext:UBLExtension>" +
                    "<ext:ExtensionContent>" +
                    "</ext:ExtensionContent>" +
                "</ext:UBLExtension>" +
              "</ext:UBLExtensions>" +
              "<cbc:UBLVersionID>" + UBLVersionID + "</cbc:UBLVersionID>" +
              "<cbc:CustomizationID>" + CustomizationID + "</cbc:CustomizationID>" +
              //"<cbc:DocumentTypeCode listID=\"UN/ECE 1001\" name=\"PE:SUNAT\" listSchemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51\">0101</cbc:DocumentTypeCode>" +
              "<cbc:ProfileID schemeName=\"SUNAT:Identificador de Tipo de Operación\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo17\">0101</cbc:ProfileID>" +
              "<cbc:ID>" + factura.NumeroComprobante + "</cbc:ID>" +
              "<cbc:IssueDate>" + factura.FechaEmision + "</cbc:IssueDate>" +
              "<cbc:IssueTime>" + factura.HoraEmision + "</cbc:IssueTime>" +
              "<cbc:InvoiceTypeCode listID=\"0101\" listAgencyName=\"PE:SUNAT\" listName=\"Tipo de Documento\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo01\">" + factura.CodigoTipoComprobante + "</cbc:InvoiceTypeCode>" +
              (factura.TotalVentaOpGratuitas > 0 ?
              "<cbc:Note languageLocaleID=\"1002\"><![CDATA[TRANSFERENCIA GRATUITA DE UN BIEN Y/O SERVICIO PRESTADO GRATUITAMENTE]]></cbc:Note>" +
              "<cbc:Note languageLocaleID=\"1000\">" + new Util().LimpiarCaracteresInvalidos2(factura.MontoEnLetras) + "</cbc:Note>"
              :
              "<cbc:Note languageLocaleID=\"1000\">" + new Util().LimpiarCaracteresInvalidos2(factura.MontoEnLetras) + "</cbc:Note>") +
              "<cbc:DocumentCurrencyCode listID=\"ISO 4217 Alpha\" listName=\"Currency\" listAgencyName=\"United Nations Economic Commission for Europe\">" + factura.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
              //"<cbc:LineCountNumeric>1</cbc:LineCountNumeric>" +
              "<cac:Signature>" +
                "<cbc:ID>IDSignKG</cbc:ID>" +
                "<cac:SignatoryParty>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID>" + factura.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + factura.NombreComercialEmisor + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                "</cac:SignatoryParty>" +
                "<cac:DigitalSignatureAttachment>" +
                  "<cac:ExternalReference>" +
                    "<cbc:URI>SignatureSP</cbc:URI>" +
                  "</cac:ExternalReference>" +
                "</cac:DigitalSignatureAttachment>" +
              "</cac:Signature>" +

              "<cac:AccountingSupplierParty>" +
                "<cbc:CustomerAssignedAccountID>" + factura.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                //"<cbc:AdditionalAccountID>" + factura.TipoDocumentoEmisor + "</cbc:AdditionalAccountID>" +
                "<cac:Party>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"" + factura.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + factura.RazonSocialEmisor + "]]></cbc:Name>" +
                  "</cac:PartyName>" +

                  //"<cac:PartyTaxScheme>" +
                  //  "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                  //  "<CompanyID schemeID=\"" + factura.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCEmisor + "</CompanyID>" +
                  //  "<cac:RegistrationAddress><cbc:AddressTypeCode>0000</cbc:AddressTypeCode></cac:RegistrationAddress>" +
                  //  "<cac:TaxScheme>" +
                  //    "<cbc:ID>-</cbc:ID>" +
                  //  "</cac:TaxScheme>" +
                  //"</cac:PartyTaxScheme>" +


                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + factura.RazonSocialEmisor + "]]></cbc:RegistrationName>" +
                    "<cac:RegistrationAddress><cbc:AddressTypeCode>0000</cbc:AddressTypeCode></cac:RegistrationAddress>" +
                  //  "<CompanyID schemeID=\"" + factura.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCEmisor + "</CompanyID>" +
                  //  "<cac:RegistrationAddress>" +
                  //    "<cbc:CityName>" + factura.DepartamentoEmisor + "</cbc:CityName>" +
                  //    "<cbc:CountrySubentity>" + factura.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                  //    "<cac:Country>" +
                  //      "<cbc:IdentificationCode>" + factura.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                  //    "</cac:Country>" +
                  //  "</cac:RegistrationAddress>" +
                  "</cac:PartyLegalEntity>" +
                //"<cac:PostalAddress>" +
                //  "<cbc:ID>" + factura.UbigeoEmisor + "</cbc:ID>" +
                //  "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionEmisor) + "]]></cbc:StreetName>" +
                //  "<cbc:CityName>" + factura.DepartamentoEmisor + "</cbc:CityName>" +
                //  "<cbc:CountrySubentity>" + factura.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                //  "<cbc:District>" + factura.DistritoEmisor + "</cbc:District>" +
                //  "<cac:Country>" +
                //    "<cbc:IdentificationCode>" + factura.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                //  "</cac:Country>" +
                //"</cac:PostalAddress>" +
                //"<cac:PartyLegalEntity>" +
                //  "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                //"</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingSupplierParty>" +
              "<cac:AccountingCustomerParty>" +
                "<cac:Party>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"" + factura.TipoDocumentoAdquirente + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCAdquirente + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  //    "<cac:PartyTaxScheme>" +
                  //      "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                  //      "<CompanyID schemeID=\"" + factura.TipoDocumentoAdquirente + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCAdquirente + "</CompanyID>" +
                  //      "<cac:TaxScheme>" +
                  //        "<cbc:ID>-</cbc:ID>" +
                  //      "</cac:TaxScheme>" +
                  //    "</cac:PartyTaxScheme>" +
                  //  //"<cac:PostalAddress>" +
                  //  //  "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(factura.DireccionAdquirente) + "</cbc:StreetName>" +
                  //  //"</cac:PostalAddress>" +
                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                  //  //  //"<cac:RegistrationAddress>" +
                  //  //  //    "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionAdquirente) + "]]></cbc:StreetName>" +
                  //  //  //"</cac:RegistrationAddress>" +
                  "</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingCustomerParty>" +
               //DETRACCIONES
               (factura.DetraccionPorcentaje > 0 ? "<cac:PaymentMeans>" +
                                                    "<cbc:PaymentMeansCode>001</cbc:PaymentMeansCode>" +
                                                    "<cac:PayeeFinancialAccount><cbc:ID>" + factura.DetraccionCuenta + "</cbc:ID></cac:PayeeFinancialAccount>" +
                                                    "</cac:PaymentMeans>" +
                                                    "<cac:PaymentTerms>" +
                                                    "<cbc:PaymentMeansID schemeName=\"Codigo de detraccion\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo54\">" + factura.DetraccionTipo + "</cbc:PaymentMeansID>" +
                                                    "<cbc:PaymentPercent>" + factura.DetraccionPorcentaje + "</cbc:PaymentPercent>" +
                                                    "<cbc:Amount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.DetraccionMonto).ToString("#0.00") + "</cbc:Amount>" +
                                                    "</cac:PaymentTerms>"
                                                : String.Empty) +

               //CAMBIO RS SUNAT 193-2020 10.03.2021
               (factura.FormaPago == "Contado" ?
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + factura.FormaPago + "</cbc:PaymentMeansID>" +
               "</cac:PaymentTerms>" :
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + factura.FormaPago + "</cbc:PaymentMeansID>" +
               "<cbc:Amount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.MontoNeto).ToString("#0.00") + "</cbc:Amount>" +
               "</cac:PaymentTerms>" +
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + factura.NumeroCuota + "</cbc:PaymentMeansID>" +
               "<cbc:Amount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.MontoCuota).ToString("#0.00") + "</cbc:Amount>" +
               "<cbc:PaymentDueDate>" + factura.FechaVencimiento + "</cbc:PaymentDueDate>" +
               "</cac:PaymentTerms>"
               ) +

              (factura.TotalDescuentos > 0 ?
               "<cac:AllowanceCharge>" +
               "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
               "<cbc:AllowanceChargeReasonCode>00</cbc:AllowanceChargeReasonCode>" +
               "<cbc:Amount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:Amount>" +
               "</cac:AllowanceCharge>" : String.Empty) +

              "<cac:TaxTotal>" +
                "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                (factura.ImporteTotalComprobante > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">S</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">1000</cbc:ID>" +
                      "<cbc:Name>IGV</cbc:Name>" +
                      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" : String.Empty) +
                (factura.TotalVentaOpGratuitas > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">Z</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9996</cbc:ID>" +
                      "<cbc:Name>GRA</cbc:Name>" +
                      "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" : String.Empty) +
                (factura.TotalVentaOpExoneradas > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">E</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                      "<cbc:Name>EXO</cbc:Name>" +
                      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                  "</cac:TaxSubtotal>" : String.Empty) +
                (factura.TotalVentaOpInafectas > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">O</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9998</cbc:ID>" +
                      "<cbc:Name>INA</cbc:Name>" +
                      "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" : String.Empty) +
              "</cac:TaxTotal>" +
              //(factura.OTRTotalComprobante > 0 && factura.TotalVentaOpGratuitas == 0 ?
              //  "<cac:TaxTotal>" +
              //      "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
              //      "<cac:TaxSubtotal>" +
              //          "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
              //          "<cac:TaxCategory>" +
              //              "<cac:TaxScheme>" +
              //                  "<cbc:ID>9999</cbc:ID>" +
              //                  "<cbc:Name>OTROS</cbc:Name>" +
              //                  "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
              //              "</cac:TaxScheme>" +
              //          "</cac:TaxCategory>" +
              //      "</cac:TaxSubtotal>" +
              //      "</cac:TaxTotal>" : String.Empty) +

              "<cac:LegalMonetaryTotal>" +
                (factura.TotalDescuentos > 0 ? "<cbc:AllowanceTotalAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:AllowanceTotalAmount>" : String.Empty) +
                (factura.OTRCTotalComprobante > 0 ? "<cbc:ChargeTotalAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRCTotalComprobante.ToString("#0.00") + "</cbc:ChargeTotalAmount>" : String.Empty) +
                "<cbc:LineExtensionAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.ImporteBrutoComprobante + factura.TotalVentaOpInafectas).ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                "<cbc:TaxInclusiveAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.ImporteTotalComprobante.ToString("#0.00") + "</cbc:TaxInclusiveAmount>" +
                //(factura.TotalVentaOpGratuitas == 0 && factura.TotalDescuentos == 0 ?
                //                    "<cbc:TaxExclusiveAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +

            "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + (factura.CodigoTipoMoneda == "USD" ?
                                                                                         factura.ImporteTotalComprobante.ToString("#0.00") :
                                                                                         factura.ImporteTotalComprobante.ToString("#0.00"))
                                                                                      + "</cbc:PayableAmount>" +
               "</cac:LegalMonetaryTotal>";
            String detalle = "";

            foreach (EDetalleFactura item in factura.DetalleFactura)
            {
                detalle = detalle + "<cac:InvoiceLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:InvoicedQuantity unitCode=\"" + item.UnidadMedida + "\" unitCodeListID=\"UN/ECE rec 20\" unitCodeListAgencyName=\"United Nations Economic Commission for Europe\">" + item.Cantidad.ToString("#0.000") + "</cbc:InvoicedQuantity>" +
                            (factura.ImporteTotalComprobante == 0 ?
                            //"<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:LineExtensionAmount>" :
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">"+ (item.ValorVentaxItem - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>" :
                            //"<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            //"<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            //"<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + ((item.Cantidad * Decimal.Round(item.PrecioUnitarioSinIGV,2)) - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            "<cac:PricingReference>" +
                              (factura.ImporteTotalComprobante == 0 ?
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.000000") + "</cbc:PriceAmount>" +
                                //"<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">02</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" :
                    //+
                    //"<cac:AlternativeConditionPrice>" +
                    //"<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" +
                    //"<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">02</cbc:PriceTypeCode>" +
                    //"</cac:AlternativeConditionPrice>" :
                                (item.IGVxItem == 0 ?
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.000000") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">01</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" :
                                (item.DescuentoxItem > 0 ?
                                "<cac:AlternativeConditionPrice>" +
                                //"<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.PrecioUnitarioIGV).ToString("#0.000000") + "</cbc:PriceAmount>" +

                                //"<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + ((item.ValorVentaxItem + item.IGVxItem)/item.Cantidad).ToString("#0.000000") + "</cbc:PriceAmount>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + ((item.ValorVentaxItem - item.DescuentoxItem + item.IGVxItem) / item.Cantidad).ToString("#0.000000") + "</cbc:PriceAmount>" +

                                "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>"
                                :
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.000000") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>"))) +

                            "</cac:PricingReference>" +
                    //SI HAY DESCUENTO
                            (item.DescuentoxItem > 0 ?
                                "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                                "<cbc:AllowanceChargeReasonCode>00</cbc:AllowanceChargeReasonCode>" +
                                "<cbc:MultiplierFactorNumeric>"+(item.PorcentajeDescuento1/100).ToString("#0.00") + "</cbc:MultiplierFactorNumeric>" +
                                "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                                "<cbc:BaseAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.PrecioUnitarioSinIGV * item.Cantidad).ToString("#0.00") + "</cbc:BaseAmount>" +
                                "</cac:AllowanceCharge>" : String.Empty) +
                    //SI HAY RECARGO
                            (item.RecargoxItem > 0 ?
                                "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>true</cbc:ChargeIndicator>" +
                                "<cbc:AllowanceChargeReasonCode>" + (item.PorcentajeDescuento1 / 100).ToString("#0.00") + "</cbc:AllowanceChargeReasonCode>" +
                                "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.RecargoxItem.ToString("#0.00") + "</cbc:Amount>" +
                                "</cac:AllowanceCharge>" : String.Empty) +
                            "<cac:TaxTotal>" +
                              (factura.ImporteTotalComprobante == 0 ?
                              //"<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" :
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" :
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") +
                              "<cac:TaxSubtotal>" +
                                (factura.ImporteTotalComprobante == 0 ?
                                (item.IGVxItem == 0 ?
                                "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>"
                                :
                                "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") :
                                //"<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>") :
                                (item.IGVxItem == 0 ? "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem + item.RecargoxItem).ToString("#0.00") + "</cbc:TaxableAmount>" :
                                //"<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem + item.RecargoxItem).ToString("#0.00") + "</cbc:TaxableAmount>") +
                                "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem + item.RecargoxItem).ToString("#0.00") + "</cbc:TaxableAmount>") +
                                (item.IGVxItem == 0 ? "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" :
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>")) +
                                "<cac:TaxCategory>" +
                                  "<cbc:Percent>" + factura.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                  "<cbc:TaxExemptionReasonCode listAgencyName=\"PE:SUNAT\" listName=\"Afectacion del IGV\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo07\">" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    (factura.ImporteTotalComprobante == 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9996</cbc:ID>" +
                                    "<cbc:Name>GRA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" :
                                    (item.IGVxItem > 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" :
                                    (item.IGVxItem == 0 && factura.TotalVentaOpExoneradas > 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                                    "<cbc:Name>EXO</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>"
                                     :
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9998</cbc:ID>" +
                                    "<cbc:Name>INA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>"))) +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              (!String.IsNullOrEmpty(item.CodigoProducto) ?
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" : String.Empty) +
                            //(item.CodigoSunat != String.Empty ?
                            //"<cac:CommodityClassification>" +
                            //"<cbc:ItemClassificationCode>" + item.CodigoSunat + "</cbc:ItemClassificationCode>" +
                            //"</cac:CommodityClassification>" : String.Empty) +
                            "</cac:Item>" +
                            "<cac:Price>" +
                                (factura.ImporteTotalComprobante == 0 ?
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0</cbc:PriceAmount>" :
                                (item.DescuentoxItem > 0 ? "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.000000") + "</cbc:PriceAmount>" :
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.000000") + "</cbc:PriceAmount>")) +
                    //"<cac:AllowanceCharge>" +
                    //  "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                    //  "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                    //"</cac:AllowanceCharge>" +
                            "</cac:Price>" +
                          "</cac:InvoiceLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</Invoice>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        private StringBuilder ArmarXmlBoletaUBL21(EBoleta boleta)
        {
            EstructuraXMLBoleta estructura = new EstructuraXMLBoleta();

            StringBuilder _xml = new StringBuilder();

            String encoding = "UTF-8";
            String UBLVersionID = "2.1";
            String CustomizationID = "2.0";

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
              "<Invoice xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" " +
              "xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" " +
              "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" " +
              "xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" " +
              "xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" " +
              "xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" " +
                //"xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" "+
              "xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" " +
              "xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\">" +
              "<ext:UBLExtensions>" +
                //"<ext:UBLExtension>" +
                //  "<ext:ExtensionContent>" +
                //    "<sac:AdditionalInformation>" +
                //      "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1001</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //      "</sac:AdditionalMonetaryTotal>" +
                //      "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1002</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //      "</sac:AdditionalMonetaryTotal>" +

                //      (factura.TotalVentaOpExoneradas > 0 ?
                //        "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>1003</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //        "</sac:AdditionalMonetaryTotal>" : String.Empty) +


                //      (factura.TotalVentaOpGratuitas > 0 ?
                //                                    "<sac:AdditionalMonetaryTotal>" +
                //                                    "<cbc:ID>1004</cbc:ID>" +

                //                                    "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                                    "</sac:AdditionalMonetaryTotal>" +
                //                                    "<sac:AdditionalProperty>" +
                //                                    "<cbc:ID>1002</cbc:ID>" +
                ////"<cbc:Value>" + factura.Motivo + " " + factura.DescripcionLey + "</cbc:Value>" +
                //                                    "<cbc:Value>" + factura.Motivo + "</cbc:Value>" +
                //                                    "</sac:AdditionalProperty>" : String.Empty) +

                //      (factura.TotalDescuentos > 0 ?
                //        "<sac:AdditionalMonetaryTotal>" +
                //        "<cbc:ID>2005</cbc:ID>" +
                //        "<cbc:PayableAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                //        "</sac:AdditionalMonetaryTotal>" : String.Empty) +

                //      "<sac:AdditionalProperty>" +
                //        "<cbc:ID>1000</cbc:ID>" +
                //        "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.MontoEnLetras) + "]]></cbc:Value>" +
                //      "</sac:AdditionalProperty>" +
                //    "</sac:AdditionalInformation>" +
                //  "</ext:ExtensionContent>" +
                //"</ext:UBLExtension>" +
                "<ext:UBLExtension>" +
                    "<ext:ExtensionContent>" +
                    "</ext:ExtensionContent>" +
                "</ext:UBLExtension>" +
              "</ext:UBLExtensions>" +
              "<cbc:UBLVersionID>" + UBLVersionID + "</cbc:UBLVersionID>" +
              "<cbc:CustomizationID>" + CustomizationID + "</cbc:CustomizationID>" +
                //"<cbc:DocumentTypeCode listID=\"UN/ECE 1001\" name=\"PE:SUNAT\" listSchemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51\">0101</cbc:DocumentTypeCode>" +

              "<cbc:ProfileID schemeName=\"SUNAT:Identificador de Tipo de Operación\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51\">0101</cbc:ProfileID>" +
              "<cbc:ID>" + boleta.NumeroComprobante + "</cbc:ID>" +
              "<cbc:IssueDate>" + boleta.FechaEmision + "</cbc:IssueDate>" +
              "<cbc:IssueTime>" + boleta.HoraEmision + "</cbc:IssueTime>" +
              "<cbc:InvoiceTypeCode listID=\"0101\" listAgencyName=\"PE:SUNAT\" listName=\"Tipo de Documento\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo01\">" + boleta.CodigoTipoComprobante + "</cbc:InvoiceTypeCode>" +
              "<cbc:Note languageLocaleID=\"1000\">" + new Util().LimpiarCaracteresInvalidos2(boleta.MontoEnLetras) + "</cbc:Note>" +
              "<cbc:DocumentCurrencyCode listID=\"ISO 4217 Alpha\" listName=\"Currency\" listAgencyName=\"United Nations Economic Commission for Europe\">" + boleta.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                //"<cbc:LineCountNumeric>1</cbc:LineCountNumeric>" +
              "<cac:Signature>" +
                "<cbc:ID>IDSignKG</cbc:ID>" +
                "<cac:SignatoryParty>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID>" + boleta.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + boleta.NombreComercialEmisor + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                "</cac:SignatoryParty>" +
                "<cac:DigitalSignatureAttachment>" +
                  "<cac:ExternalReference>" +
                    "<cbc:URI>SignatureSP</cbc:URI>" +
                  "</cac:ExternalReference>" +
                "</cac:DigitalSignatureAttachment>" +
              "</cac:Signature>" +

              "<cac:AccountingSupplierParty>" +
                //"<cbc:CustomerAssignedAccountID>" + factura.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                //"<cbc:AdditionalAccountID>" + factura.TipoDocumentoEmisor + "</cbc:AdditionalAccountID>" +
                  "<cac:Party>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"" + boleta.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + boleta.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                //"<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.RazonSocialEmisor) + "]]></cbc:Name>" +
                    "<cbc:Name><![CDATA[" + boleta.RazonSocialEmisor + "]]></cbc:Name>" +
                  "</cac:PartyName>" +

                  //"<cac:PartyTaxScheme>" +
                //  "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                //  "<CompanyID schemeID=\"" + factura.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCEmisor + "</CompanyID>" +
                //  "<cac:RegistrationAddress><cbc:AddressTypeCode>0000</cbc:AddressTypeCode></cac:RegistrationAddress>" +
                //  "<cac:TaxScheme>" +
                //    "<cbc:ID>-</cbc:ID>" +
                //  "</cac:TaxScheme>" +
                //"</cac:PartyTaxScheme>" +


                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + boleta.RazonSocialEmisor + "]]></cbc:RegistrationName>" +
                    "<cac:RegistrationAddress><cbc:AddressTypeCode>0000</cbc:AddressTypeCode></cac:RegistrationAddress>" +
                //  "<CompanyID schemeID=\"" + factura.TipoDocumentoEmisor + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCEmisor + "</CompanyID>" +
                //  "<cac:RegistrationAddress>" +
                //    "<cbc:CityName>" + factura.DepartamentoEmisor + "</cbc:CityName>" +
                //    "<cbc:CountrySubentity>" + factura.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                //    "<cac:Country>" +
                //      "<cbc:IdentificationCode>" + factura.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                //    "</cac:Country>" +
                //  "</cac:RegistrationAddress>" +
                  "</cac:PartyLegalEntity>" +
                //"<cac:PostalAddress>" +
                //  "<cbc:ID>" + factura.UbigeoEmisor + "</cbc:ID>" +
                //  "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionEmisor) + "]]></cbc:StreetName>" +
                //  "<cbc:CityName>" + factura.DepartamentoEmisor + "</cbc:CityName>" +
                //  "<cbc:CountrySubentity>" + factura.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                //  "<cbc:District>" + factura.DistritoEmisor + "</cbc:District>" +
                //  "<cac:Country>" +
                //    "<cbc:IdentificationCode>" + factura.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                //  "</cac:Country>" +
                //"</cac:PostalAddress>" +
                //"<cac:PartyLegalEntity>" +
                //  "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                //"</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingSupplierParty>" +

              "<cac:AccountingCustomerParty>" +
               "<cac:Party>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"" + boleta.TipoDocumentoAdquirente + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + boleta.RUCAdquirente + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                //    "<cac:PartyTaxScheme>" +
                //      "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                //      "<CompanyID schemeID=\"" + factura.TipoDocumentoAdquirente + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + factura.RUCAdquirente + "</CompanyID>" +
                //      "<cac:TaxScheme>" +
                //        "<cbc:ID>-</cbc:ID>" +
                //      "</cac:TaxScheme>" +
                //    "</cac:PartyTaxScheme>" +
                //  //"<cac:PostalAddress>" +
                //  //  "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(factura.DireccionAdquirente) + "</cbc:StreetName>" +
                //  //"</cac:PostalAddress>" +
                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                //  //  //"<cac:RegistrationAddress>" +
                //  //  //    "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(factura.DireccionAdquirente) + "]]></cbc:StreetName>" +
                //  //  //"</cac:RegistrationAddress>" +
                  "</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingCustomerParty>" +

               //CAMBIO RS SUNAT 193-2020 10.03.2021
               (boleta.FormaPago == "Contado" ?
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + boleta.FormaPago + "</cbc:PaymentMeansID>" +
               "</cac:PaymentTerms>" :
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + boleta.FormaPago + "</cbc:PaymentMeansID>" +
               "<cbc:Amount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + (boleta.MontoNeto).ToString("#0.00") + "</cbc:Amount>" +
               "</cac:PaymentTerms>" +
               "<cac:PaymentTerms>" +
               "<cbc:ID>FormaPago</cbc:ID>" +
               "<cbc:PaymentMeansID>" + boleta.NumeroCuota + "</cbc:PaymentMeansID>" +
               "<cbc:Amount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + (boleta.MontoCuota).ToString("#0.00") + "</cbc:Amount>" +
               "<cbc:PaymentDueDate>" + boleta.FechaVencimiento + "</cbc:PaymentDueDate>" +
               "</cac:PaymentTerms>"
               ) +

              "<cac:TaxTotal>" +
                "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +

                 (boleta.ImporteTotalComprobante > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpGravadas + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">S</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">1000</cbc:ID>" +
                      "<cbc:Name>IGV</cbc:Name>" +
                      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                // "</cac:TaxSubtotal>" : String.Empty) +
               "</cac:TaxSubtotal>" :
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">Z</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9996</cbc:ID>" +
                      "<cbc:Name>GRA</cbc:Name>" +
                      "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>") +
                (boleta.TotalVentaOpExoneradas > 0 ?
                "<cac:TaxSubtotal>" +
                //"<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxableAmount>" +
                //"<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:TaxAmount>" +
                  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">E</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                      "<cbc:Name>EXO</cbc:Name>" +
                      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                  "</cac:TaxSubtotal>" : String.Empty) +
                (boleta.TotalVentaOpInafectas > 0 ?
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">O</cbc:ID>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9998</cbc:ID>" +
                      "<cbc:Name>INA</cbc:Name>" +
                      "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" : String.Empty) +

              //"<cac:TaxSubtotal>" +
                //  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxableAmount>" +
                //  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                //  "<cac:TaxCategory>" +
                //    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">S</cbc:ID>" +
                //    "<cac:TaxScheme>" +
                //      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">1000</cbc:ID>" +
                //      "<cbc:Name>IGV</cbc:Name>" +
                //      "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                //    "</cac:TaxScheme>" +
                //  "</cac:TaxCategory>" +
                //"</cac:TaxSubtotal>" +
                //"<cac:TaxSubtotal>" +
                //  "<cbc:TaxableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                //  "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                //  "<cac:TaxCategory>" +
                //    "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">Z</cbc:ID>" +
                //    "<cac:TaxScheme>" +
                //      "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9996</cbc:ID>" +
                //      "<cbc:Name>GRA</cbc:Name>" +
                //      "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                //    "</cac:TaxScheme>" +
                //  "</cac:TaxCategory>" +
                //"</cac:TaxSubtotal>" +

              "</cac:TaxTotal>" +
                //(factura.OTRTotalComprobante > 0 && factura.TotalVentaOpGratuitas == 0 ?
                //  "<cac:TaxTotal>" +
                //      "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                //      "<cac:TaxSubtotal>" +
                //          "<cbc:TaxAmount currencyID=\"" + factura.CodigoTipoMoneda + "\">" + factura.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                //          "<cac:TaxCategory>" +
                //              "<cac:TaxScheme>" +
                //                  "<cbc:ID>9999</cbc:ID>" +
                //                  "<cbc:Name>OTROS</cbc:Name>" +
                //                  "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
                //              "</cac:TaxScheme>" +
                //          "</cac:TaxCategory>" +
                //      "</cac:TaxSubtotal>" +
                //      "</cac:TaxTotal>" : String.Empty) +

              "<cac:LegalMonetaryTotal>" +
                (boleta.TotalDescuentos > 0 ? "<cbc:AllowanceTotalAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalDescuentos.ToString("#0.00") + "</cbc:AllowanceTotalAmount>" : String.Empty) +
                (boleta.OTRCTotalComprobante > 0 ? "<cbc:ChargeTotalAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.OTRCTotalComprobante.ToString("#0.00") + "</cbc:ChargeTotalAmount>" : String.Empty) +

                (boleta.TotalVentaOpGratuitas == 0 && boleta.TotalDescuentos == 0 ?

                                    "<cbc:LineExtensionAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + (boleta.ImporteBrutoComprobante + boleta.TotalVentaOpInafectas).ToString("#0.00") + "</cbc:LineExtensionAmount>" +




                                    "<cbc:TaxExclusiveAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +

                "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + (boleta.CodigoTipoMoneda == "USD" ?
                                                                                         boleta.ImporteBrutoComprobante.ToString("#0.00") :
                                                                                         boleta.ImporteTotalComprobante.ToString("#0.00"))
                                                                                      + "</cbc:PayableAmount>" +
               "</cac:LegalMonetaryTotal>";// +
            //"<cbc:InvoiceTypeCode listID=\"UN/ECE 1001\" name=\"PE:SUNAT\" listSchemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51\">0101</cbc:InvoiceTypeCode>";

            String detalle = "";

            foreach (EDetalleBoleta item in boleta.DetalleBoleta)
            {
                detalle = detalle + "<cac:InvoiceLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:InvoicedQuantity unitCode=\"" + item.UnidadMedida + "\" unitCodeListID=\"UN/ECE rec 20\" unitCodeListAgencyName=\"United Nations Economic Commission for Europe\">" + item.Cantidad.ToString("#0.000") + "</cbc:InvoicedQuantity>" +
                            (boleta.ImporteTotalComprobante == 0 ?
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:LineExtensionAmount>" :
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + (item.ValorVentaxItem - item.DescuentoxItem).ToString("#0.00") + "</cbc:LineExtensionAmount>") +
                            "<cac:PricingReference>" +
                              (boleta.ImporteTotalComprobante == 0 ?
                              "<cac:AlternativeConditionPrice>" +
                                    "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" +
                                    "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">02</cbc:PriceTypeCode>" +
                              "</cac:AlternativeConditionPrice>" :
                              "<cac:AlternativeConditionPrice>" +
                                    "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                    "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">01</cbc:PriceTypeCode>" +
                                 "</cac:AlternativeConditionPrice>") +
                            "</cac:PricingReference>" +
                    //"<cac:AllowanceCharge>" +
                    //  "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                    //  "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                    //"</cac:AllowanceCharge>" +
                            "<cac:TaxTotal>" +
                              (boleta.ImporteTotalComprobante == 0 && item.IGVxItem == 0 ?
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" :
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") +
                              "<cac:TaxSubtotal>" +

                                (boleta.TotalVentaOpInafectas > 0 && item.IGVxItem == 0 ?
                                  "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                  "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                                  "<cac:TaxCategory>" +
                                      "<cbc:Percent>" + boleta.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                      "<cbc:TaxExemptionReasonCode listAgencyName=\"PE:SUNAT\" listName=\"Afectacion del IGV\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo07\">" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                       "<cac:TaxScheme>" +
                                        "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9998</cbc:ID>" +
                                        "<cbc:Name>INA</cbc:Name>" +
                                        "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                                      "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" :

                                (boleta.ImporteTotalComprobante == 0 ?
                                    "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                    (item.IGVxItem == 0 ?
                                        "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>"
                                        :
                                        "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>")
                                    :
                                    "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                    "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>") +
                                "<cac:TaxCategory>" +
                                  "<cbc:Percent>" + boleta.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                  "<cbc:TaxExemptionReasonCode listAgencyName=\"PE:SUNAT\" listName=\"Afectacion del IGV\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo07\">" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +

                                   "<cac:TaxScheme>" +
                                    (boleta.ImporteTotalComprobante == 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9996</cbc:ID>" +
                                    "<cbc:Name>GRA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" :
                                        (boleta.TotalVentaOpExoneradas > 0 ?
                                         "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                                        "<cbc:Name>EXO</cbc:Name>" +
                                        "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" :
                                         "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">1000</cbc:ID>" +
                                        "<cbc:Name>IGV</cbc:Name>" +
                                        "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>")) +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>") +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                    //(item.CodigoSunat != String.Empty ?
                    //"<cac:CommodityClassification>" +
                    //"<cbc:ItemClassificationCode>" + item.CodigoSunat + "</cbc:ItemClassificationCode>" +
                    //"</cac:CommodityClassification>" : String.Empty) +
                            "</cac:Item>" +
                            "<cac:Price>" +
                                (boleta.ImporteTotalComprobante == 0 ?
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" :
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.00") + "</cbc:PriceAmount>") +
                    //"<cac:AllowanceCharge>" +
                    //  "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                    //  "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                    //"</cac:AllowanceCharge>" +
                            "</cac:Price>" +
                          "</cac:InvoiceLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</Invoice>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        public EResultado GenerarFacturaPDF(EFactura factura)
        {
            EResultado eresult = new EResultado();

            string strFormatoFechaEmision = factura.FechaEmision.Substring(0, 4) + factura.FechaEmision.Substring(5, 2) + factura.FechaEmision.Substring(8, 2);
            int intFechaEmision = int.Parse(strFormatoFechaEmision);
            string strEnviarFactura = ConfigurationManager.AppSettings["EnviarFactura"];
            factura.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA
            factura.MensajeAgenteRetencion = ConfigurationManager.AppSettings["MENSAJE_AGENTE_RETENCION"]; //ADD MMILLA
            if (intFechaEmision >= 20180401)
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION_2018"]; //ADD MMILLA
            }
            else
            {
                factura.MensajeDetraccion = ConfigurationManager.AppSettings["MENSAJE_DETRACCION"]; //ADD MMILLA
            }

            factura.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            factura.TipoDocumentoEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            factura.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            factura.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            factura.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            factura.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            factura.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            factura.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            factura.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            factura.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            factura.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            factura.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();
            //factura.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //factura.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            try
            {
                Boolean exito = true;
                //CONEXION GESPO
                string strCadenaConexionGESPO = ConfigurationManager.ConnectionStrings["ConexionGespo"].ConnectionString;
                string strCero = "0";
                factura.Observaciones = "";
                //GRATUITA - MENSAJE - LEY
                if (factura.ImporteTotalComprobante == 0)
                {
                    //TOTAL DE OPERACIONES GRATUITAS
                    string strSQLMensaje = "select vchDescripcion, a.vchObservaciones from sgp_comprobante a, SGP_DETALLE_TABLA b where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' and a.intTipotablaG = b.numCodigo and b.vchValor = a.intTipoBaseLegalG";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.DescripcionLey = dtMensaje.Rows[0]["vchDescripcion"].ToString();
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }
                else
                {
                    string strSQLMensaje = "select a.vchObservaciones from sgp_comprobante a where a.vchNumSerie = '" + factura.ComprobanteSerie + "' and a.vchNumComprobante = '" + factura.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' ";
                    SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                    DataTable dtMensaje = new DataTable();
                    da.Fill(dtMensaje);
                    if (dtMensaje.Rows.Count > 0)
                    {
                        factura.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                    }
                }

                String filenamePDF = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + ".pdf";
                String filenamePDF_ = factura.RUCEmisor + "-" + factura.CodigoTipoComprobante + "-" + factura.NumeroComprobante.Split('-')[0] + "-" + factura.NumeroComprobante.Split('-')[1] + "_.pdf";

                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                //SETEAR POR FECHA DE EMISION
                path += @"\" + factura.FechaEmision.Substring(0, 4) + @"\" + factura.FechaEmision.Substring(5, 2) + @"\" + factura.FechaEmision.Substring(8, 2);

                //Armar PDF
                filenamePDF_ = path + "/" + filenamePDF_;
                filenamePDF = path + "/" + filenamePDF;

                if (File.Exists(filenamePDF))
                    File.Delete(filenamePDF);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                //Generar PDF
                int intNumeroLineasMax = int.Parse(ConfigurationManager.AppSettings["LINEAS_MAX_PAGINA"].ToString());
                int intLineaDetalle = factura.DetalleFactura.Count();
                int intNumPaginas = 1;
                if (intLineaDetalle % intNumeroLineasMax == 0)
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax);
                else
                    intNumPaginas = (intLineaDetalle / intNumeroLineasMax) + 1;

                FabricaSingleton<FacturaPDF>.Crear().GenerarPDF(filenamePDF_, factura, intNumPaginas, intNumeroLineasMax);
                itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                try
                {
                    exito = true;
                    eresult.CDR = "0";
                    eresult.MensajeResultadoSunat = filenamePDF;
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    String mensajesunat = "";
                    System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                    if (dtmensaje.Rows.Count > 0)
                    {
                        mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                    }

                    eresult.CDR = ex.Code.Name;
                    eresult.EstadoComprobante = "RECHAZADO";
                    if (!String.IsNullOrEmpty(mensajesunat))
                        eresult.MensajeResultadoSunat = mensajesunat;
                    else
                        eresult.MensajeResultadoSunat = ex.Message.Replace("'", "");
                    if (ex.Message.Length > 215)
                        eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                    exito = false;
                }
            }
            catch (Exception ex)
            {
                Escribir_Log(ex.Message);
            }

            return eresult;
        }

        #endregion

        #region Enviar Boleta a Sunat

        public EResultado EnviarBoletaOSE(EBoleta boleta)
        {
            Boolean exito = true;
            string EnviarBoleta = ConfigurationManager.AppSettings["EnviarBoleta"];

            boleta.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA
            boleta.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            boleta.TipoDocumentoEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"]; //6:Registro Unico Contribuyentes
            boleta.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            boleta.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            boleta.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            boleta.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            boleta.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            boleta.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            boleta.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            boleta.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            boleta.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            boleta.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();

            //boleta.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //boleta.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            string strCadenaConexionGESPO = ConfigurationManager.ConnectionStrings["ConexionGespo"].ConnectionString;
            string strCero = "0";
            boleta.Observaciones = "";
            //GRATUITA - MENSAJE - LEY
            if (boleta.ImporteTotalComprobante == 0)
            {
                //TOTAL DE OPERACIONES GRATUITAS
                string strSQLMensaje = "select vchDescripcion,a.vchObservaciones from sgp_comprobante a, SGP_DETALLE_TABLA b where a.vchNumSerie = '" + boleta.ComprobanteSerie + "' and a.vchNumComprobante = '" + boleta.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' and a.intTipotablaG = b.numCodigo and b.vchValor = a.intTipoBaseLegalG";
                SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                DataTable dtMensaje = new DataTable();
                da.Fill(dtMensaje);
                if (dtMensaje.Rows.Count > 0)
                {
                    boleta.DescripcionLey = dtMensaje.Rows[0]["vchDescripcion"].ToString();
                    boleta.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                }
            }
            else
            {
                string strSQLMensaje = "select a.vchObservaciones from sgp_comprobante a where a.vchNumSerie = '" + boleta.ComprobanteSerie + "' and a.vchNumComprobante = '" + boleta.ComprobanteCorrelativo.PadLeft(7, strCero[0]) + "' ";
                SqlDataAdapter da = new SqlDataAdapter(strSQLMensaje, new SqlConnection(strCadenaConexionGESPO));
                DataTable dtMensaje = new DataTable();
                da.Fill(dtMensaje);
                if (dtMensaje.Rows.Count > 0)
                {
                    boleta.Observaciones = dtMensaje.Rows[0]["vchObservaciones"].ToString();
                }
            }

            //StringBuilder _xml = ArmarXmlBoleta(boleta);
            StringBuilder _xml = ArmarXmlBoletaUBL21(boleta);


            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Guardar xml
            String filenameXml = boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + ".xml";
            String filenameRptaXml = "R-" + boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + ".xml";
            String filenameZip = boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + ".zip";
            String filenameRptaZip = "R-" + boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + ".zip";

            String filenamePDF = boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + ".pdf";
            String filenamePDF_ = boleta.RUCEmisor + "-" + boleta.CodigoTipoComprobante + "-" + boleta.NumeroComprobante.Split('-')[0] + "-" + boleta.NumeroComprobante.Split('-')[1] + "_.pdf";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            //SETEAR POR FECHA DE EMISION
            path += @"\" + boleta.FechaEmision.Substring(0, 4) + @"\" + boleta.FechaEmision.Substring(5, 2) + @"\" + boleta.FechaEmision.Substring(8, 2);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            String tmpXml = path + "/" + filenameXml;

            filenameZip = path + "/" + filenameZip;
            filenameRptaZip = path + "/" + filenameRptaZip;
            filenameRptaXml = path + "/" + filenameRptaXml;

            //Guardar Xml
            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            new Util().SaveData(xmlsunat, tmpXml);

            //Fimar Xml
            String SignatureValue = "";
            String DigestValue = "";
            //new FirmaDigital().FirmarXml(tmpXml, "Invoice", ref SignatureValue, ref DigestValue);
            new FirmaDigital().FirmarXmlUBL21(tmpXml, "Invoice", ref SignatureValue, ref DigestValue);

            boleta.SignatureValue = SignatureValue;
            boleta.DigestValue = DigestValue;

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);
            /////////////
            //Armar PDF
            filenamePDF_ = path + "/" + filenamePDF_;
            filenamePDF = path + "/" + filenamePDF;

            if (File.Exists(filenamePDF))
                File.Delete(filenamePDF);

            if (File.Exists(filenamePDF_))
                File.Delete(filenamePDF_);

            //Generar PDF
            int intNumeroLineasMax = int.Parse(ConfigurationManager.AppSettings["LINEAS_MAX_PAGINA"].ToString());
            int intLineaDetalle = boleta.DetalleBoleta.Count();
            int intNumPaginas = 1;
            if (intLineaDetalle % intNumeroLineasMax == 0)
                intNumPaginas = (intLineaDetalle / intNumeroLineasMax);
            else
                intNumPaginas = (intLineaDetalle / intNumeroLineasMax) + 1;

            FabricaSingleton<BoletaPDF>.Crear().GenerarPDF(filenamePDF_, boleta, intNumPaginas, intNumeroLineasMax);
            itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

            if (File.Exists(filenamePDF_))
                File.Delete(filenamePDF_);
            ////////////////////////

            EResultado eresult = new EResultado();

            try
            {
                if (EnviarBoleta == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;
                    //TLS1.2
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                    //OSE
                    ClientCredentials clientCredentials = new ClientCredentials();
                    clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                    clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                    var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                    proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                    proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                    proxy.Close();

                    if (File.Exists(filenameRptaZip))
                        File.Delete(filenameRptaZip);

                    if (File.Exists(filenameRptaXml))
                        File.Delete(filenameRptaXml);

                    new Util().SaveData(oSalida, filenameRptaZip);

                    using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                    {
                        zip.ExtractAll(path);
                    }

                    //Leer Xml Respuesta Sunat
                    XmlDocument xmlDocRptaSunat = new XmlDocument();
                    xmlDocRptaSunat.Load(filenameRptaXml);

                    XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                    XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                    XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                    string responsecode = String.Empty, description = String.Empty;

                    foreach (XmlNode nodo in lista)
                    {
                        responsecode = nodo["cbc:ResponseCode"].InnerText;
                        description = nodo["cbc:Description"].InnerText;
                    }

                    String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                    eresult.CDR = responsecode;
                    eresult.EstadoComprobante = EstadoComprobante;
                    eresult.MensajeResultadoSunat = description;

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                }
                else
                {
                    exito = true;
                    eresult.EstadoComprobante = "GENERADO";
                    eresult.CDR = "0";
                    eresult.MensajeResultadoSunat = "Boleta ha sido generada";
                }
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }
                else
                    mensajesunat = ex.Message;

                if (!String.IsNullOrEmpty(mensajesunat))
                    mensajesunat = mensajesunat.Replace("'", "");
                else
                    mensajesunat = ex.Message.Replace("'", "");

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = "RECHAZADO";
                eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                exito = false;
            }

            if (exito)
            {
                //filenamePDF_ = path + "/" + filenamePDF_;
                //filenamePDF = path + "/" + filenamePDF;

                //if (File.Exists(filenamePDF))
                //    File.Delete(filenamePDF);

                //if (File.Exists(filenamePDF_))
                //    File.Delete(filenamePDF_);

                ////Generar PDF
                //FabricaSingleton<BoletaPDF>.Crear().GenerarPDF(filenamePDF_, boleta);
                //itextUtil.FirmarPDF(filenamePDF_, filenamePDF,_HuellaFirmante,false);

                //if (File.Exists(filenamePDF_))
                //    File.Delete(filenamePDF_);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlBoleta(EBoleta boleta)
        {
            EstructuraXMLBoleta estructura = new EstructuraXMLBoleta();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];
            String UBLVersionID = ConfigurationManager.AppSettings["UBLVersionID"];
            String CustomizationID = ConfigurationManager.AppSettings["CustomizationID"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                            "<sac:AdditionalInformation>" +
                                                "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1001</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                  "</sac:AdditionalMonetaryTotal>" +
                                                  "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1002</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                  "</sac:AdditionalMonetaryTotal>" +

                                                  (boleta.TotalVentaOpExoneradas > 0 ?
                                                    "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1003</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" : String.Empty) +

                                                  (boleta.TotalVentaOpGratuitas > 0 ?
                                                    "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1004</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" +
                                                    "<sac:AdditionalProperty>" +
                                                    "<cbc:ID>1002</cbc:ID>" +
                                                    "<cbc:Value>" + boleta.Motivo + " " + boleta.DescripcionLey + "</cbc:Value>" +
                                                    "</sac:AdditionalProperty>" : String.Empty) +

                                                  (boleta.TotalDescuentos > 0 ?
                                                    "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>2005</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" : String.Empty) +

                                                  "<sac:AdditionalProperty>" +
                                                    "<cbc:ID>1000</cbc:ID>" +
                                                    "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.MontoEnLetras) + "]]></cbc:Value>" +
                                                  "</sac:AdditionalProperty>" +
                                            "</sac:AdditionalInformation>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>" + UBLVersionID + "</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>" + CustomizationID + "</cbc:CustomizationID>" +
                                "<cbc:ID>" + boleta.NumeroComprobante + "</cbc:ID>" +
                                "<cbc:IssueDate>" + boleta.FechaEmision + "</cbc:IssueDate>" +
                                "<cbc:InvoiceTypeCode>" + boleta.CodigoTipoComprobante + "</cbc:InvoiceTypeCode>" +
                                "<cbc:DocumentCurrencyCode>" + boleta.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + boleta.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>SignatureSP</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                  "</cac:Signature>" +
                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + boleta.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + boleta.TipoDocumentoEmisor + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                      "<cac:PostalAddress>" +
                                        "<cbc:ID>" + boleta.UbigeoEmisor + "</cbc:ID>" +
                                        "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(boleta.DireccionEmisor) + "</cbc:StreetName>" +
                //"<cbc:CitySubdivisionName>" + new Util().LimpiarCaracteresInvalidos(boleta.UrbanizacionEmisor) + "</cbc:CitySubdivisionName>" +
                                        "<cbc:CityName>" + boleta.DepartamentoEmisor + "</cbc:CityName>" +
                                        "<cbc:CountrySubentity>" + boleta.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                                        "<cbc:District>" + boleta.DistritoEmisor + "</cbc:District>" +
                                        "<cac:Country>" +
                                          "<cbc:IdentificationCode>" + boleta.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                                        "</cac:Country>" +
                                      "</cac:PostalAddress>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>" +
                                "<cac:AccountingCustomerParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + boleta.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + boleta.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" +
                                        "<cac:RegistrationAddress>" +
                                                "<cbc:StreetName>" +
                                                    "<![CDATA[" + new Util().LimpiarCaracteresInvalidos(boleta.DireccionAdquirente) + "]]>" +
                                                "</cbc:StreetName>" +
                                                "<cac:Country>" +
                                                    "<cbc:IdentificationCode>" + boleta.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                                                "</cac:Country>" +
                                            "</cac:RegistrationAddress>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingCustomerParty>" +
                                "<cac:TaxTotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID>1000</cbc:ID>" +
                                          "<cbc:Name>IGV</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" +
                                "</cac:TaxTotal>" +

                                (boleta.OTRTotalComprobante > 0 && boleta.TotalVentaOpGratuitas == 0 ?
                                "<cac:TaxTotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxSubtotal>" +
                                        "<cbc:TaxAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.OTRTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                        "<cac:TaxCategory>" +
                                            "<cac:TaxScheme>" +
                                                "<cbc:ID>9999</cbc:ID>" +
                                                "<cbc:Name>OTROS</cbc:Name>" +
                                                "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
                                            "</cac:TaxScheme>" +
                                        "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" +
                                    "</cac:TaxTotal>" : String.Empty) +

                                "<cac:LegalMonetaryTotal>" +
                                    (boleta.TotalDescuentos > 0 ? "<cbc:AllowanceTotalAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.TotalDescuentos.ToString("#0.00") + "</cbc:AllowanceTotalAmount>" : String.Empty) +
                                    (boleta.OTRCTotalComprobante > 0 ? "<cbc:ChargeTotalAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.OTRCTotalComprobante.ToString("#0.00") + "</cbc:ChargeTotalAmount>" : String.Empty) +

                                    (boleta.TotalVentaOpGratuitas == 0 && boleta.TotalDescuentos == 0 ?
                                    "<cbc:LineExtensionAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.ImporteBrutoComprobante.ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                                    "<cbc:TaxExclusiveAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +








                                    "<cbc:PayableAmount currencyID=\"" + boleta.CodigoTipoMoneda + "\">" + boleta.ImporteTotalComprobante.ToString("#0.00") + "</cbc:PayableAmount>" +



                                "</cac:LegalMonetaryTotal>";

            String detalle = "";

            foreach (EDetalleBoleta item in boleta.DetalleBoleta)
            {
                detalle = detalle + "<cac:InvoiceLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:InvoicedQuantity unitCode=\"" + item.UnidadMedida + "\">" + item.Cantidad.ToString("#0.0000") + "</cbc:InvoicedQuantity>" +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                            "<cac:PricingReference>" +

                              (boleta.ImporteTotalComprobante == 0 ?
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">0.00</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>01</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" +
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>02</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>" :
                                "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                                "</cac:AlternativeConditionPrice>") +

                            "</cac:PricingReference>" +
                            "<cac:TaxTotal>" +
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                              "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                    //"<cbc:Percent>" + boleta.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                "<cac:TaxCategory>" +
                                  "<cbc:TaxExemptionReasonCode>" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    "<cbc:ID>1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                            "</cac:Item>" +
                            "<cac:Price>" +
                              "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorUnitarioxItem.ToString("#0.00") + "</cbc:PriceAmount>" +
                              "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>false</cbc:ChargeIndicator>" +
                                "<cbc:Amount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.DescuentoxItem.ToString("#0.00") + "</cbc:Amount>" +
                              "</cac:AllowanceCharge>" +
                            "</cac:Price>" +
                        "</cac:InvoiceLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</Invoice>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        #endregion

        #region Enviar Nota Credito

        public EResultado EnviarNotaCreditoOSE(ENotaCredito notacredito)
        {
            Boolean exito = true;
            string EnviarCredito = ConfigurationManager.AppSettings["EnviarCredito"];

            //Datos del Emisor
            notacredito.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA 05.11.2015
            notacredito.MensajeAgenteRetencion = ConfigurationManager.AppSettings["MENSAJE_AGENTE_RETENCION"]; //ADD MMILLA 05.11.2015
            notacredito.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            notacredito.CodigoTipoDocEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            notacredito.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            notacredito.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            notacredito.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            notacredito.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            notacredito.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            notacredito.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            notacredito.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            notacredito.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            notacredito.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            notacredito.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();
            //notacredito.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //notacredito.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            StringBuilder _xml = ArmarXmlNotaCreditoUBL21(notacredito);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Registrar Solicitud
            //Int64 idsolicitud = FabricaSingleton<DComprobante>.Crear().RegistrarSolicitud("Nota Credito"
            //                                                                            , notacredito.CodigoTipoNotaCredito
            //                                                                            , notacredito.SerieNumeroCorrelativo
            //                                                                            , notacredito.FechaEmision
            //                                                                            , String.Empty //boleta.HoraEmision
            //                                                                            , notacredito.CodigoTipoMoneda
            //                                                                            , notacredito.RUCEmisor
            //                                                                            , notacredito.CodigoTipoDocEmisor
            //                                                                            , notacredito.RazonSocialEmisor
            //                                                                            , notacredito.NombreComercialEmisor
            //                                                                            , notacredito.UbigeoEmisor
            //                                                                            , notacredito.DireccionEmisor
            //                                                                            , notacredito.UrbanizacionEmisor
            //                                                                            , notacredito.DepartamentoEmisor
            //                                                                            , notacredito.ProvinciaEmisor
            //                                                                            , notacredito.DistritoEmisor
            //                                                                            , notacredito.CodigoPaisEmisor
            //                                                                            , notacredito.RUCAdquirente
            //                                                                            , notacredito.TipoDocumentoAdquirente
            //                                                                            , notacredito.RazonSocialAdquirente
            //                                                                            , notacredito.DireccionAdquirente
            //                                                                            , notacredito.IGVTotalComprobante
            //                                                                            , notacredito.ImporteTotalComprobante
            //                                                                            , String.Empty //notacredito.CodigoTipoDocumentoReferencia
            //                                                                            , String.Empty //notacredito.NumeroDocumentoReferencia
            //                                                                            , notacredito.SerieNumeroDocAfectado
            //                                                                            , notacredito.SerieNumeroDocAfectado
            //                                                                            , notacredito.MotivoSustentoDocumento);

            //foreach (EDetalleNotaCredito det in notacredito.DetalleNotaCredito)
            //{
            //    FabricaSingleton<DComprobante>.Crear().RegistrarDetalleSolicitud(idsolicitud
            //                                                                   , Convert.ToInt32(det.NumeroItem)
            //                                                                   , det.Cantidad
            //                                                                   , det.DescripcionProducto
            //                                                                   , det.CodigoProducto
            //                                                                   , det.ValorUnitarioxItem
            //                                                                   , det.PrecioUnitario
            //                                                                   , det.IGVxItem
            //                                                                   , det.ValorVentaxItem);
            //}

            //Guardar xml
            String filenameXml = notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + ".xml";
            String filenameRptaXml = "R-" + notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + ".xml";
            String filenameZip = notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";
            String filenameRptaZip = "R-" + notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";

            String filenamePDF = notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + ".pdf";
            String filenamePDF_ = notacredito.RUCEmisor + "-" + notacredito.CodigoTipoNotaCredito + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notacredito.SerieNumeroCorrelativo.Split('-')[1] + "_.pdf";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            //SETEAR POR FECHA DE EMISION
            path += @"\" + notacredito.FechaEmision.Substring(0, 4) + @"\" + notacredito.FechaEmision.Substring(5, 2) + @"\" + notacredito.FechaEmision.Substring(8, 2);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            String tmpXml = path + "/" + filenameXml;

            filenameZip = path + "/" + filenameZip;
            filenameRptaZip = path + "/" + filenameRptaZip;
            filenameRptaXml = path + "/" + filenameRptaXml;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar Xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Fimar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXmlUBL21(tmpXml, "CreditNote", ref SignatureValue, ref DigestValue);

            notacredito.SignatureValue = SignatureValue;
            notacredito.DigestValue = DigestValue;

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (EnviarCredito == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;
                    //TLS1.2
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    //OSE
                    ClientCredentials clientCredentials = new ClientCredentials();
                    clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                    clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                    var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                    proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                    proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                    proxy.Close();

                    if (File.Exists(filenameRptaZip))
                        File.Delete(filenameRptaZip);

                    if (File.Exists(filenameRptaXml))
                        File.Delete(filenameRptaXml);

                    new Util().SaveData(oSalida, filenameRptaZip);

                    using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                    {
                        zip.ExtractAll(path);
                    }

                    //Leer Xml Respuesta Sunat
                    XmlDocument xmlDocRptaSunat = new XmlDocument();
                    xmlDocRptaSunat.Load(filenameRptaXml);

                    XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                    XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                    XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                    string responsecode = String.Empty, description = String.Empty;

                    foreach (XmlNode nodo in lista)
                    {
                        responsecode = nodo["cbc:ResponseCode"].InnerText;
                        description = nodo["cbc:Description"].InnerText;
                    }

                    String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                    eresult.CDR = responsecode;
                    eresult.EstadoComprobante = EstadoComprobante;
                    eresult.MensajeResultadoSunat = description.Replace("'", "");

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                }
                else
                {
                    exito = true;
                    eresult.CDR = "0";
                }
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }
                else
                {
                    mensajesunat = ex.ToString();
                }

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = "RECHAZADO";
                //eresult.MensajeResultadoSunat = mensajesunat;

                if (!String.IsNullOrEmpty(mensajesunat))
                    eresult.MensajeResultadoSunat = mensajesunat.Replace("'", "");
                else
                    eresult.MensajeResultadoSunat = ex.Message.Replace("'", "");
                if (ex.Message.Length > 215)
                    eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                exito = false;
            }

            if (exito)
            {
                filenamePDF_ = path + "/" + filenamePDF_;
                filenamePDF = path + "/" + filenamePDF;

                if (File.Exists(filenamePDF))
                    File.Delete(filenamePDF);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                //Generar PDF
                FabricaSingleton<NotaCreditoPDF>.Crear().GenerarPDF(filenamePDF_, notacredito);
                itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlNotaCredito(ENotaCredito notacredito)
        {
            EstructuraXMLNotaCredito estructura = new EstructuraXMLNotaCredito();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<CreditNote xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                            "<sac:AdditionalInformation>" +

                                                (notacredito.TotalVentaOpGravadas > 0 ?
                                                "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1001</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                "</sac:AdditionalMonetaryTotal>" : "") +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>1002</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +
                                                  (notacredito.TotalVentaOpExoneradas > 0 ?
                                                  "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1003</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" : "") +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>1004</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>2005</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +
                                                  "<sac:AdditionalProperty>" +
                                                    "<cbc:ID>1000</cbc:ID>" +
                                                    "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.MontoEnLetras) + "]]></cbc:Value>" +
                                                  "</sac:AdditionalProperty>" +
                                            "</sac:AdditionalInformation>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + notacredito.SerieNumeroCorrelativo + "</cbc:ID>" +
                                "<cbc:IssueDate>" + notacredito.FechaEmision + "</cbc:IssueDate>" +
                                "<cbc:DocumentCurrencyCode>" + notacredito.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                                "<cac:DiscrepancyResponse>" +
                                                "<cbc:ReferenceID>" + notacredito.SerieNumeroDocAfectado + "</cbc:ReferenceID>" + //<!-- Serie y nÃºmero de documento afectado) -->
                                                "<cbc:ResponseCode>" + notacredito.CodigoMotivoNotaCredito + "</cbc:ResponseCode>" + //<!-- CÃ³digo de tipo de nota de crÃ©dito -->
                                                "<cbc:Description>" + new Util().LimpiarCaracteresInvalidos(notacredito.MotivoSustentoDocumento) + "</cbc:Description>" + //<!-- Motivo o Sustento -->
                                "</cac:DiscrepancyResponse>" +
                "<cac:BillingReference>" +
                                "<cac:InvoiceDocumentReference>" +
                                    "<cbc:ID>" + notacredito.SerieNumeroDocAfectado + "</cbc:ID>" + // <!-- Serie y nÃºmero del documento que modifica -->
                                    "<cbc:DocumentTypeCode>" + notacredito.CodigoTipoDocAfectado + "</cbc:DocumentTypeCode>" + //<!-- Tipo de documento del documento que modifica -->
                                "</cac:InvoiceDocumentReference>" +
                "</cac:BillingReference>" +

            "<cac:Signature>" +
                "<cbc:ID>IDSignKG</cbc:ID>" +
                "<cac:SignatoryParty>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID>" + notacredito.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.NombreComercialEmisor) + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                "</cac:SignatoryParty>" +
                "<cac:DigitalSignatureAttachment>" +
                  "<cac:ExternalReference>" +
                    "<cbc:URI>signatureKG</cbc:URI>" +
                  "</cac:ExternalReference>" +
                "</cac:DigitalSignatureAttachment>" +
            "</cac:Signature>" +

            "<cac:AccountingSupplierParty>" +
                "<cbc:CustomerAssignedAccountID>" + notacredito.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                "<cbc:AdditionalAccountID>" + notacredito.CodigoTipoDocEmisor + "</cbc:AdditionalAccountID>" +
                "<cac:Party>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.NombreComercialEmisor) + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                  "<cac:PostalAddress>" +
                    "<cbc:ID>" + notacredito.UbigeoEmisor + "</cbc:ID>" +
                    "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(notacredito.DireccionEmisor) + "</cbc:StreetName>" +
                //"<cbc:CitySubdivisionName>" + new Util().LimpiarCaracteresInvalidos(notacredito.UrbanizacionEmisor) + "</cbc:CitySubdivisionName>" +
                    "<cbc:CityName>" + notacredito.DepartamentoEmisor + "</cbc:CityName>" +
                    "<cbc:CountrySubentity>" + notacredito.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                    "<cbc:District>" + notacredito.DistritoEmisor + "</cbc:District>" +
                    "<cac:Country>" +
                      "<cbc:IdentificationCode>" + notacredito.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                    "</cac:Country>" +
                  "</cac:PostalAddress>" +
                  "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                  "</cac:PartyLegalEntity>" +
                "</cac:Party>" +
              "</cac:AccountingSupplierParty>" +

            "<cac:AccountingCustomerParty>" +//<!-- Datos del domicilio del adquirente o usuario -->
                            "<cbc:CustomerAssignedAccountID>" + notacredito.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" + //<!-- RUC del Adquirente o usuario-->
                            "<cbc:AdditionalAccountID>" + notacredito.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +  //<!-- Tipo de Documento de Identificacion del Adquirente o usuario(6:Registro Unico Contribuyentes)-->
                            "<cac:Party>" +
                                "<cac:PartyLegalEntity>" +
                                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" + //<!-- Razon Social del Adquirente o usuario -->
                                "</cac:PartyLegalEntity>" +
                            "</cac:Party>" +
            "</cac:AccountingCustomerParty>" +

            "<cac:TaxTotal>" +
                "<cbc:TaxAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                "<cac:TaxSubtotal>" +
                  "<cbc:TaxAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                  "<cac:TaxCategory>" +
                    "<cac:TaxScheme>" +
                      "<cbc:ID>1000</cbc:ID>" +
                      "<cbc:Name>IGV</cbc:Name>" +
                    "</cac:TaxScheme>" +
                  "</cac:TaxCategory>" +
                "</cac:TaxSubtotal>" +
            "</cac:TaxTotal>" +
            "<cac:LegalMonetaryTotal>" +
                "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.ImporteTotalComprobante.ToString("#0.00") + "</cbc:PayableAmount>" +
            "</cac:LegalMonetaryTotal>";

            String detalle = "";

            foreach (EDetalleNotaCredito item in notacredito.DetalleNotaCredito)
            {
                detalle = detalle + "<cac:CreditNoteLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:CreditedQuantity unitCode=\"" + item.UnidadMedida + "\">" + item.Cantidad.ToString("#0.000") + "</cbc:CreditedQuantity>" +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                            "<cac:PricingReference>" +
                              "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                              "</cac:AlternativeConditionPrice>" +
                            "</cac:PricingReference>" +
                            "<cac:TaxTotal>" +
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                    //"<cbc:Percent>" + notacredito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                              "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxCategory>" +
                                  "<cbc:TaxExemptionReasonCode>" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    "<cbc:ID>1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                            "</cac:Item>" +
                            "<cac:Price>" +
                              "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorUnitarioxItem.ToString("#0.00") + "</cbc:PriceAmount>" +
                            "</cac:Price>" +
                        "</cac:CreditNoteLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</CreditNote>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        private StringBuilder ArmarXmlNotaCreditoUBL21(ENotaCredito notacredito)
        {
            EstructuraXMLNotaCredito estructura = new EstructuraXMLNotaCredito();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<CreditNote xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                //"<ext:UBLExtension>" +
                //    "<ext:ExtensionContent>" +
                //        "<sac:AdditionalInformation>" +

                                    //            (notacredito.TotalVentaOpGravadas > 0 ?
                //            "<sac:AdditionalMonetaryTotal>" +
                //                "<cbc:ID>1001</cbc:ID>" +
                //                "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //            "</sac:AdditionalMonetaryTotal>" : "") +
                //            //"<sac:AdditionalMonetaryTotal>" +
                //            //  "<cbc:ID>1002</cbc:ID>" +
                //            //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //            //"</sac:AdditionalMonetaryTotal>" +
                //              (notacredito.TotalVentaOpExoneradas > 0 ?
                //              "<sac:AdditionalMonetaryTotal>" +
                //                "<cbc:ID>1003</cbc:ID>" +
                //                "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                "</sac:AdditionalMonetaryTotal>" : "") +
                //                //"<sac:AdditionalMonetaryTotal>" +
                //                //  "<cbc:ID>1004</cbc:ID>" +
                //                //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpGratuitas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                //"</sac:AdditionalMonetaryTotal>" +
                //                //"<sac:AdditionalMonetaryTotal>" +
                //                //  "<cbc:ID>2005</cbc:ID>" +
                //                //  "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalDescuentos.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                //"</sac:AdditionalMonetaryTotal>" +
                //              "<sac:AdditionalProperty>" +
                //                "<cbc:ID>1000</cbc:ID>" +
                //                "<cbc:Value><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.MontoEnLetras) + "]]></cbc:Value>" +
                //              "</sac:AdditionalProperty>" +
                //        "</sac:AdditionalInformation>" +
                //    "</ext:ExtensionContent>" +
                //"</ext:UBLExtension>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +

                                "<cbc:UBLVersionID>2.1</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>2.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + notacredito.SerieNumeroCorrelativo + "</cbc:ID>" +
                                "<cbc:IssueDate>" + notacredito.FechaEmision + "</cbc:IssueDate>" +
                                "<cbc:IssueTime>12:00:05</cbc:IssueTime>" +
                                "<cbc:DocumentCurrencyCode>" + notacredito.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                                "<cac:DiscrepancyResponse>" +
                                                "<cbc:ReferenceID>" + notacredito.SerieNumeroDocAfectado + "</cbc:ReferenceID>" + //<!-- Serie y nÃºmero de documento afectado) -->
                                                "<cbc:ResponseCode>" + notacredito.CodigoTipoNotaCredito + "</cbc:ResponseCode>" + //<!-- CÃ³digo de tipo de nota de crÃ©dito -->
                                                "<cbc:Description>" + new Util().LimpiarCaracteresInvalidos(notacredito.MotivoSustentoDocumento) + "</cbc:Description>" + //<!-- Motivo o Sustento -->
                                "</cac:DiscrepancyResponse>" +
                                "<cac:BillingReference>" +
                                    "<cac:InvoiceDocumentReference>" +
                                        "<cbc:ID>" + notacredito.SerieNumeroDocAfectado + "</cbc:ID>" + // <!-- Serie y nÃºmero del documento que modifica -->
                                        "<cbc:DocumentTypeCode>" + notacredito.CodigoTipoDocAfectado + "</cbc:DocumentTypeCode>" + //<!-- Tipo de documento del documento que modifica -->
                                    "</cac:InvoiceDocumentReference>" +
                                "</cac:BillingReference>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + notacredito.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +


                                "<cac:AccountingSupplierParty>" +
                //"<cbc:CustomerAssignedAccountID>" + notacredito.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                //"<cbc:AdditionalAccountID>" + notacredito.CodigoTipoDocEmisor + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID schemeID=\"6\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + notacredito.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                //"<cac:PartyTaxScheme>" +
                //  "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.NombreComercialEmisor) + "]]></cbc:RegistrationName>" +
                //  "<CompanyID schemeID=\"6\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + notacredito.RUCEmisor + "</CompanyID>" +
                //  "<cac:RegistrationAddress>" +
                //    "<cbc:AddressTypeCode>0001</cbc:AddressTypeCode>" +
                //  "</cac:RegistrationAddress>" +
                //  "<cac:TaxScheme>" +
                //    "<cbc:ID>-</cbc:ID>" +
                //  "</cac:TaxScheme>" +
                //"</cac:PartyTaxScheme>"+

                                      //"<cac:PostalAddress>" +
                //  "<cbc:ID>" + notacredito.UbigeoEmisor + "</cbc:ID>" +
                //  "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(notacredito.DireccionEmisor) + "</cbc:StreetName>" +
                //  //"<cbc:CitySubdivisionName>" + new Util().LimpiarCaracteresInvalidos(notacredito.UrbanizacionEmisor) + "</cbc:CitySubdivisionName>" +
                //  "<cbc:CityName>" + notacredito.DepartamentoEmisor + "</cbc:CityName>" +
                //  "<cbc:CountrySubentity>" + notacredito.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                //  "<cbc:District>" + notacredito.DistritoEmisor + "</cbc:District>" +
                //  "<cac:Country>" +
                //    "<cbc:IdentificationCode>" + notacredito.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                //  "</cac:Country>" +
                //"</cac:PostalAddress>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                                        "<cac:RegistrationAddress>" +
                                          "<cbc:AddressTypeCode>0001</cbc:AddressTypeCode>" +
                                        "</cac:RegistrationAddress>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>" +

                                  "<cac:AccountingCustomerParty>" +//<!-- Datos del domicilio del adquirente o usuario -->
                //"<cbc:CustomerAssignedAccountID>" + notacredito.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" + //<!-- RUC del Adquirente o usuario-->
                //"<cbc:AdditionalAccountID>" + notacredito.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +  //<!-- Tipo de Documento de Identificacion del Adquirente o usuario(6:Registro Unico Contribuyentes)-->
                                    "<cac:Party>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID schemeID=\"" + notacredito.TipoDocumentoAdquirente + "\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + notacredito.RUCAdquirente + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notacredito.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" + //<!-- Razon Social del Adquirente o usuario -->
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingCustomerParty>" +
                                "<cac:TaxTotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    (notacredito.ImporteTotalComprobante > 0 ?
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpGravadas + "</cbc:TaxableAmount>" +
                                      "<cbc:TaxAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">S</cbc:ID>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">1000</cbc:ID>" +
                                          "<cbc:Name>IGV</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" : String.Empty) +
                                    (notacredito.TotalVentaOpExoneradas > 0 ?
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                      "<cbc:TaxAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">E</cbc:ID>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                                          "<cbc:Name>EXO</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                      "</cac:TaxSubtotal>" : String.Empty) +
                                "</cac:TaxTotal>" +
                                "<cac:LegalMonetaryTotal>" +
                                    (notacredito.TotalVentaOpGratuitas == 0 && notacredito.TotalDescuentos == 0 ?
                                    "<cbc:LineExtensionAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + (notacredito.ImporteBrutoComprobante + notacredito.TotalVentaOpInafectas).ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                                    "<cbc:TaxExclusiveAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +
                                    "<cbc:PayableAmount currencyID=\"" + notacredito.CodigoTipoMoneda + "\">" + notacredito.ImporteTotalComprobante.ToString("#0.00") + "</cbc:PayableAmount>" +
                                "</cac:LegalMonetaryTotal>";

            String detalle = "";

            foreach (EDetalleNotaCredito item in notacredito.DetalleNotaCredito)
            {
                detalle = detalle + "<cac:CreditNoteLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:CreditedQuantity unitCode=\"" + item.UnidadMedida + "\" unitCodeListID=\"UN/ECE rec 20\" unitCodeListAgencyName=\"United Nations Economic Commission for Europe\">" + item.Cantidad.ToString("#0.000") + "</cbc:CreditedQuantity>" +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                            "<cac:PricingReference>" +
                              "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.000") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode listName=\"Tipo de Precio\" listAgencyName=\"PE:SUNAT\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16\">" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                              "</cac:AlternativeConditionPrice>" +
                            "</cac:PricingReference>" +
                            "<cac:TaxTotal>" +
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                    //"<cbc:Percent>" + notacredito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                              "<cac:TaxSubtotal>" +
                                "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxCategory>" +
                    //"<cbc:ID>S</cbc:ID>" +
                                  "<cbc:Percent>" + notacredito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                  "<cbc:TaxExemptionReasonCode listAgencyName=\"PE:SUNAT\" listName=\"Afectacion del IGV\" listURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo07\">" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    (notacredito.ImporteTotalComprobante == 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9996</cbc:ID>" +
                                    "<cbc:Name>GRA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" :
                                    (item.IGVxItem > 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" :
                                    (item.IGVxItem == 0 && notacredito.TotalVentaOpExoneradas > 0 ?
                                    "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                                    "<cbc:Name>EXO</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>"
                                     :
                                     "<cbc:ID schemeID=\"UN/ECE 5305\" schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9998</cbc:ID>" +
                                    "<cbc:Name>INA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>"))) +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                    //(item.CodigoSunat != String.Empty ?
                    //"<cac:CommodityClassification>" +
                    //"<cbc:ItemClassificationCode>" + item.CodigoSunat + "</cbc:ItemClassificationCode>" +
                    //"</cac:CommodityClassification>" : String.Empty) +
                            "</cac:Item>" +
                            "<cac:Price>" +
                              "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioSinIGV.ToString("#0.000000") + "</cbc:PriceAmount>" +
                            "</cac:Price>" +
                        "</cac:CreditNoteLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</CreditNote>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }
        #endregion

        #region Enviar Nota Debito

        public EResultado EnviarNotaDebitoOSE(ENotaDebito notadebito)
        {
            Boolean exito = true;
            string EnviarDebito = ConfigurationManager.AppSettings["EnviarDebito"];

            //Datos del Emisor
            notadebito.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_RESOLUCION_SUNAT"]; //ADD MMILLA 05.11.2015
            notadebito.MensajeAgenteRetencion = ConfigurationManager.AppSettings["MENSAJE_AGENTE_RETENCION"]; //ADD MMILLA 05.11.2015
            notadebito.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            notadebito.CodigoTipoDocEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            notadebito.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            notadebito.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            notadebito.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            notadebito.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            notadebito.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            notadebito.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            notadebito.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            notadebito.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            notadebito.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            notadebito.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();
            //notadebito.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_EDITORA"];
            //notadebito.DireccionAlternativaEmisor = ConfigurationManager.AppSettings["DIRECCION_ALTERNATIVA_EDITORA"];

            StringBuilder _xml = ArmarXmlNotaDebitoUBL21(notadebito);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);


            String filenameXml = notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".xml";
            String filenameRptaXml = "R-" + notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".xml";
            String filenameZip = notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";
            String filenameRptaZip = "R-" + notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";

            String filenamePDF = notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".pdf";
            String filenamePDF_ = notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + "_.pdf";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            //SETEAR POR FECHA DE EMISION
            path += @"\" + notadebito.FechaEmision.Substring(0, 4) + @"\" + notadebito.FechaEmision.Substring(5, 2) + @"\" + notadebito.FechaEmision.Substring(8, 2);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            String tmpXml = path + "/" + filenameXml;

            filenameZip = path + "/" + filenameZip;
            filenameRptaZip = path + "/" + filenameRptaZip;
            filenameRptaXml = path + "/" + filenameRptaXml;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Firmar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXmlUBL21(tmpXml, "DebitNote", ref SignatureValue, ref DigestValue);

            notadebito.SignatureValue = SignatureValue;
            notadebito.DigestValue = DigestValue;

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (EnviarDebito == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;
                    //TLS1.2
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    //OSE
                    ClientCredentials clientCredentials = new ClientCredentials();
                    clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                    clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                    var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                    proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                    proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                    proxy.Close();

                    if (File.Exists(filenameRptaZip))
                        File.Delete(filenameRptaZip);

                    if (File.Exists(filenameRptaXml))
                        File.Delete(filenameRptaXml);

                    new Util().SaveData(oSalida, filenameRptaZip);

                    using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                    {
                        zip.ExtractAll(path);
                    }

                    //Leer Xml Respuesta Sunat
                    XmlDocument xmlDocRptaSunat = new XmlDocument();
                    xmlDocRptaSunat.Load(filenameRptaXml);

                    XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                    XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                    XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                    string responsecode = String.Empty, description = String.Empty;

                    foreach (XmlNode nodo in lista)
                    {
                        responsecode = nodo["cbc:ResponseCode"].InnerText;
                        description = nodo["cbc:Description"].InnerText;
                    }

                    String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                    eresult.CDR = responsecode;
                    eresult.EstadoComprobante = EstadoComprobante;
                    eresult.MensajeResultadoSunat = description.Replace("'", "");
                    if (eresult.MensajeResultadoSunat.Length > 215)
                        eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
                }
                else
                {
                    exito = true;
                    eresult.CDR = "0";
                }
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = "RECHAZADO";

                if (!String.IsNullOrEmpty(mensajesunat))
                    eresult.MensajeResultadoSunat = mensajesunat.Replace("'", "");
                else
                    eresult.MensajeResultadoSunat = ex.Message.Replace("'", "");
                if (ex.Message.Length > 215)
                    eresult.MensajeResultadoSunat = eresult.MensajeResultadoSunat.Substring(0, 215);

                //eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                exito = false;
            }

            if (exito)
            {
                filenamePDF_ = path + "/" + filenamePDF_;
                filenamePDF = path + "/" + filenamePDF;

                if (File.Exists(filenamePDF))
                    File.Delete(filenamePDF);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);

                //Generar PDF
                FabricaSingleton<NotaDebitoPDF>.Crear().GenerarPDF(filenamePDF_, notadebito);
                itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                if (File.Exists(filenamePDF_))
                    File.Delete(filenamePDF_);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlNotaDebito(ENotaDebito notadebito)
        {
            EstructuraXMLNotaDebito estructura = new EstructuraXMLNotaDebito();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<DebitNote xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                            "<sac:AdditionalInformation>" +

                                                (notadebito.TotalVentaOpGravadas > 0 ?
                                                "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1001</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                "</sac:AdditionalMonetaryTotal>" : "") +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>1002</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +

                                                  (notadebito.TotalVentaOpExoneradas > 0 ?
                                                  "<sac:AdditionalMonetaryTotal>" +
                                                    "<cbc:ID>1003</cbc:ID>" +
                                                    "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                                                    "</sac:AdditionalMonetaryTotal>" : "") +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>1004</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpGratuitas + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +
                //"<sac:AdditionalMonetaryTotal>" +
                //  "<cbc:ID>2005</cbc:ID>" +
                //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalDescuentos + "</cbc:PayableAmount>" +
                //"</sac:AdditionalMonetaryTotal>" +
                //"<sac:AdditionalProperty>" +
                //  "<cbc:ID>1000</cbc:ID>" +
                //  "<cbc:Value>" + notadebito.MontoEnLetras + "</cbc:Value>" +
                //"</sac:AdditionalProperty>" +
                                            "</sac:AdditionalInformation>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + notadebito.SerieNumeroCorrelativo + "</cbc:ID>" +
                                "<cbc:IssueDate>" + notadebito.FechaEmision + "</cbc:IssueDate>" +
                                "<cbc:DocumentCurrencyCode>" + notadebito.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                                "<cac:DiscrepancyResponse>" +
                                                (notadebito.CodigoMotivoNotaDebito != "03" ?
                                                "<cbc:ReferenceID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ReferenceID>" :
                                                "<cbc:ReferenceID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ReferenceID>") + //<!-- Serie y nÃºmero de documento afectado) -->
                                                "<cbc:ResponseCode>" + notadebito.CodigoMotivoNotaDebito + "</cbc:ResponseCode>" + //<!-- CÃ³digo de tipo de nota de crÃ©dito -->
                                                "<cbc:Description>" + new Util().LimpiarCaracteresInvalidos(notadebito.MotivoSustentoDocumento) + "</cbc:Description>" + //<!-- Motivo o Sustento -->
                                "</cac:DiscrepancyResponse>" +
                                "<cac:BillingReference>" +
                                                "<cac:InvoiceDocumentReference>" +
                                                    (notadebito.CodigoMotivoNotaDebito != "03" ?
                                                    "<cbc:ID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ID>" + // <!-- Serie y nÃºmero del documento que modifica -->
                                                    "<cbc:DocumentTypeCode>" + notadebito.CodigoTipoDocAfectado + "</cbc:DocumentTypeCode>" //<!-- Tipo de documento del documento que modifica -->
                                                    :
                                                    "<cbc:ID>-</cbc:ID>" +
                                                    "<cbc:DocumentTypeCode></cbc:DocumentTypeCode>") +
                                                "</cac:InvoiceDocumentReference>" +
                                "</cac:BillingReference>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + notadebito.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + notadebito.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + notadebito.CodigoTipoDocEmisor + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                      "<cac:PostalAddress>" +
                                        "<cbc:ID>" + notadebito.UbigeoEmisor + "</cbc:ID>" +
                                        "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(notadebito.DireccionEmisor) + "</cbc:StreetName>" +
                //"<cbc:CitySubdivisionName>" + new Util().LimpiarCaracteresInvalidos(notadebito.UrbanizacionEmisor) + "</cbc:CitySubdivisionName>" +
                                        "<cbc:CityName>" + notadebito.DepartamentoEmisor + "</cbc:CityName>" +
                                        "<cbc:CountrySubentity>" + notadebito.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                                        "<cbc:District>" + notadebito.DistritoEmisor + "</cbc:District>" +
                                        "<cac:Country>" +
                                          "<cbc:IdentificationCode>" + notadebito.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                                        "</cac:Country>" +
                                      "</cac:PostalAddress>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>" +

                                "<cac:AccountingCustomerParty>" +//<!-- Datos del domicilio del adquirente o usuario -->
                                                "<cbc:CustomerAssignedAccountID>" + notadebito.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" + //<!-- RUC del Adquirente o usuario-->
                                                "<cbc:AdditionalAccountID>" + notadebito.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +  //<!-- Tipo de Documento de Identificacion del Adquirente o usuario(6:Registro Unico Contribuyentes)-->
                                                "<cac:Party>" +
                                                    "<cac:PartyLegalEntity>" +
                                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" + //<!-- Razon Social del Adquirente o usuario -->
                                                    "</cac:PartyLegalEntity>" +
                                                "</cac:Party>" +
                                "</cac:AccountingCustomerParty>" +

                                (notadebito.CodigoMotivoNotaDebito == "01" ?
                                "<cac:TaxTotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID>1000</cbc:ID>" +
                                          "<cbc:Name>IGV</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" +
                                    "</cac:TaxTotal>" : String.Empty) +

                                "<cac:RequestedMonetaryTotal>" +
                                    "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.ImporteTotalComprobante.ToString("#0.00") + "</cbc:PayableAmount>" +
                                "</cac:RequestedMonetaryTotal>";

            String detalle = "";

            foreach (EDetalleNotaDebito item in notadebito.DetalleNotaDebito)
            {
                detalle = detalle + "<cac:DebitNoteLine>" +
                            "<cbc:ID>" + item.NumeroItem + "</cbc:ID>" +
                            "<cbc:DebitedQuantity unitCode=\"" + item.UnidadMedida + "\">" + item.Cantidad.ToString("#0.000") + "</cbc:DebitedQuantity>" +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem + "</cbc:LineExtensionAmount>" +
                            "<cac:PricingReference>" +
                              "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                              "</cac:AlternativeConditionPrice>" +
                            "</cac:PricingReference>" +
                            "<cac:TaxTotal>" +
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                    //"<cbc:Percent>" + notadebito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                              "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxCategory>" +
                                  "<cbc:TaxExemptionReasonCode>" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    "<cbc:ID>1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                            "</cac:Item>" +
                            "<cac:Price>" +
                              "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorUnitarioxItem.ToString("#0.00") + "</cbc:PriceAmount>" +
                            "</cac:Price>" +
                        "</cac:DebitNoteLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</DebitNote>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        private StringBuilder ArmarXmlNotaDebitoUBL21(ENotaDebito notadebito)
        {
            EstructuraXMLNotaDebito estructura = new EstructuraXMLNotaDebito();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<DebitNote xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                //"<ext:UBLExtension>" +
                //    "<ext:ExtensionContent>" +
                //        "<sac:AdditionalInformation>" +

                                    //            (notadebito.TotalVentaOpGravadas > 0 ?
                //            "<sac:AdditionalMonetaryTotal>" +
                //                "<cbc:ID>1001</cbc:ID>" +
                //                "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpGravadas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //            "</sac:AdditionalMonetaryTotal>" : "") +
                //            //"<sac:AdditionalMonetaryTotal>" +
                //            //  "<cbc:ID>1002</cbc:ID>" +
                //            //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //            //"</sac:AdditionalMonetaryTotal>" +

                                    //              (notadebito.TotalVentaOpExoneradas > 0 ?
                //              "<sac:AdditionalMonetaryTotal>" +
                //                "<cbc:ID>1003</cbc:ID>" +
                //                "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:PayableAmount>" +
                //                "</sac:AdditionalMonetaryTotal>" : "") +
                //                //"<sac:AdditionalMonetaryTotal>" +
                //                //  "<cbc:ID>1004</cbc:ID>" +
                //                //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpGratuitas + "</cbc:PayableAmount>" +
                //                //"</sac:AdditionalMonetaryTotal>" +
                //                //"<sac:AdditionalMonetaryTotal>" +
                //                //  "<cbc:ID>2005</cbc:ID>" +
                //                //  "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalDescuentos + "</cbc:PayableAmount>" +
                //                //"</sac:AdditionalMonetaryTotal>" +
                //                //"<sac:AdditionalProperty>" +
                //                //  "<cbc:ID>1000</cbc:ID>" +
                //                //  "<cbc:Value>" + notadebito.MontoEnLetras + "</cbc:Value>" +
                //                //"</sac:AdditionalProperty>" +
                //        "</sac:AdditionalInformation>" +
                //    "</ext:ExtensionContent>" +
                //"</ext:UBLExtension>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.1</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>2.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + notadebito.SerieNumeroCorrelativo + "</cbc:ID>" +
                                "<cbc:IssueDate>" + notadebito.FechaEmision + "</cbc:IssueDate>" +
                                "<cbc:IssueTime>12:00:05</cbc:IssueTime>" +
                                "<cbc:DocumentCurrencyCode>" + notadebito.CodigoTipoMoneda + "</cbc:DocumentCurrencyCode>" +
                                "<cac:DiscrepancyResponse>" +
                                                (notadebito.CodigoMotivoNotaDebito != "03" ?
                                                "<cbc:ReferenceID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ReferenceID>" :
                                                "<cbc:ReferenceID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ReferenceID>") + //<!-- Serie y nÃºmero de documento afectado) -->
                                                "<cbc:ResponseCode>" + notadebito.CodigoMotivoNotaDebito + "</cbc:ResponseCode>" + //<!-- CÃ³digo de tipo de nota de crÃ©dito -->
                                                "<cbc:Description>" + new Util().LimpiarCaracteresInvalidos(notadebito.MotivoSustentoDocumento) + "</cbc:Description>" + //<!-- Motivo o Sustento -->
                                "</cac:DiscrepancyResponse>" +
                                (notadebito.CodigoMotivoNotaDebito == "03" ? "" :
                                "<cac:BillingReference>" +
                                                "<cac:InvoiceDocumentReference>" +
                                                    (notadebito.CodigoMotivoNotaDebito != "03" ?
                                                    "<cbc:ID>" + notadebito.SerieNumeroDocAfectado + "</cbc:ID>" + // <!-- Serie y nÃºmero del documento que modifica -->
                                                    "<cbc:DocumentTypeCode>" + notadebito.CodigoTipoDocAfectado + "</cbc:DocumentTypeCode>" //<!-- Tipo de documento del documento que modifica -->
                                                    :
                                                    //"<cbc:ID>0-0</cbc:ID>" +
                                                    //"<cbc:DocumentTypeCode></cbc:DocumentTypeCode>") +
                                                    "") +
                                                "</cac:InvoiceDocumentReference>" +
                                "</cac:BillingReference>") +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + notadebito.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                //"<cbc:CustomerAssignedAccountID>" + notadebito.RUCEmisor + "</cbc:CustomerAssignedAccountID>" +
                //"<cbc:AdditionalAccountID>" + notadebito.CodigoTipoDocEmisor + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID schemeID=\"6\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + notadebito.RUCEmisor + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.NombreComercialEmisor) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                //"<cac:PartyName>" +
                //  "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.NombreComercialEmisor) + "]]></cbc:Name>" +
                //"</cac:PartyName>" +
                //"<cac:PostalAddress>" +
                //  "<cbc:ID>" + notadebito.UbigeoEmisor + "</cbc:ID>" +
                //  "<cbc:StreetName>" + new Util().LimpiarCaracteresInvalidos(notadebito.DireccionEmisor) + "</cbc:StreetName>" +
                //  //"<cbc:CitySubdivisionName>" + new Util().LimpiarCaracteresInvalidos(notadebito.UrbanizacionEmisor) + "</cbc:CitySubdivisionName>" +
                //  "<cbc:CityName>" + notadebito.DepartamentoEmisor + "</cbc:CityName>" +
                //  "<cbc:CountrySubentity>" + notadebito.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                //  "<cbc:District>" + notadebito.DistritoEmisor + "</cbc:District>" +
                //  "<cac:Country>" +
                //    "<cbc:IdentificationCode>" + notadebito.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                //  "</cac:Country>" +
                //"</cac:PostalAddress>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                                        "<cac:RegistrationAddress>" +
                                          "<cbc:AddressTypeCode>0001</cbc:AddressTypeCode>" +
                                        "</cac:RegistrationAddress>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>" +

                                "<cac:AccountingCustomerParty>" +//<!-- Datos del domicilio del adquirente o usuario -->
                //"<cbc:CustomerAssignedAccountID>" + notadebito.RUCAdquirente + "</cbc:CustomerAssignedAccountID>" + //<!-- RUC del Adquirente o usuario-->
                //"<cbc:AdditionalAccountID>" + notadebito.TipoDocumentoAdquirente + "</cbc:AdditionalAccountID>" +  //<!-- Tipo de Documento de Identificacion del Adquirente o usuario(6:Registro Unico Contribuyentes)-->
                                    "<cac:Party>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID schemeID=\"6\" schemeName=\"Documento de Identidad\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06\">" + notadebito.RUCAdquirente + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(notadebito.RazonSocialAdquirente) + "]]></cbc:RegistrationName>" + //<!-- Razon Social del Adquirente o usuario -->
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                "</cac:AccountingCustomerParty>" +

                                "<cac:TaxTotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    (notadebito.ImporteTotalComprobante > 0 ?
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpGravadas + "</cbc:TaxableAmount>" +
                                      "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">S</cbc:ID>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">1000</cbc:ID>" +
                                          "<cbc:Name>IGV</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" : String.Empty) +
                                    (notadebito.TotalVentaOpExoneradas > 0 ?
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpExoneradas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                      "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">E</cbc:ID>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID schemeName=\"Codigo de tributos\" schemeAgencyName=\"PE:SUNAT\" schemeURI=\"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05\">9997</cbc:ID>" +
                                          "<cbc:Name>EXO</cbc:Name>" +
                                          "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                      "</cac:TaxSubtotal>" : String.Empty) +
                                      (notadebito.TotalVentaOpInafectas > 0 ?
                                    "<cac:TaxSubtotal>" +
                                      "<cbc:TaxableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.TotalVentaOpInafectas.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                      "<cbc:TaxAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">0.00</cbc:TaxAmount>" +
                                      "<cac:TaxCategory>" +
                                        "<cbc:ID schemeAgencyName=\"United Nations Economic Commission for Europe\" schemeID=\"UN/ECE 5305\" schemeName=\"Tax Category Identifier\">O</cbc:ID>" +
                                        "<cac:TaxScheme>" +
                                          "<cbc:ID schemeAgencyID=\"6\" schemeID=\"UN/ECE 5153\">9998</cbc:ID>" +
                                          "<cbc:Name>INA</cbc:Name>" +
                                          "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                      "</cac:TaxCategory>" +
                                    "</cac:TaxSubtotal>" : String.Empty) +
                                    "</cac:TaxTotal>" +
                                "<cac:RequestedMonetaryTotal>" +
                                    (notadebito.TotalVentaOpGratuitas == 0 && notadebito.TotalDescuentos == 0 ?
                                    "<cbc:LineExtensionAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + (notadebito.ImporteBrutoComprobante + notadebito.TotalVentaOpInafectas).ToString("#0.00") + "</cbc:LineExtensionAmount>" +
                                    "<cbc:TaxExclusiveAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.IGVTotalComprobante.ToString("#0.00") + "</cbc:TaxExclusiveAmount>" : String.Empty) +
                                    "<cbc:PayableAmount currencyID=\"" + notadebito.CodigoTipoMoneda + "\">" + notadebito.ImporteTotalComprobante.ToString("#0.00") + "</cbc:PayableAmount>" +
                                    "</cac:RequestedMonetaryTotal>";

            String detalle = "";
            int intLinea = 1;
            foreach (EDetalleNotaDebito item in notadebito.DetalleNotaDebito)
            {
                detalle = detalle + "<cac:DebitNoteLine>" +
                            "<cbc:ID>" + intLinea.ToString() + "</cbc:ID>" +
                            "<cbc:DebitedQuantity unitCode=\"" + item.UnidadMedida + "\">" + item.Cantidad.ToString("#0.000") + "</cbc:DebitedQuantity>" +
                            "<cbc:LineExtensionAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem + "</cbc:LineExtensionAmount>" +
                            "<cac:PricingReference>" +
                              "<cac:AlternativeConditionPrice>" +
                                "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.PrecioUnitarioIGV.ToString("#0.00") + "</cbc:PriceAmount>" +
                                "<cbc:PriceTypeCode>" + item.CodigoTipoPrecio + "</cbc:PriceTypeCode>" +
                              "</cac:AlternativeConditionPrice>" +
                            "</cac:PricingReference>" +
                            "<cac:TaxTotal>" +
                              "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                    //"<cbc:Percent>" + notadebito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                              "<cac:TaxSubtotal>" +
                                "<cbc:TaxableAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:TaxableAmount>" +
                                "<cbc:TaxAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.IGVxItem.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxCategory>" +
                                  "<cbc:Percent>" + notadebito.ValorIGV.ToString("#0.0") + "</cbc:Percent>" +
                                  "<cbc:TaxExemptionReasonCode>" + item.CodigoTipoIGV + "</cbc:TaxExemptionReasonCode>" +
                                  "<cac:TaxScheme>" +
                                    (item.IGVxItem > 0 ?
                                    "<cbc:ID>1000</cbc:ID>" +
                                    "<cbc:Name>IGV</cbc:Name>" +
                                    "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" :
                                    "<cbc:ID>9998</cbc:ID>" +
                                    "<cbc:Name>INA</cbc:Name>" +
                                    "<cbc:TaxTypeCode>FRE</cbc:TaxTypeCode>") +
                                  "</cac:TaxScheme>" +
                                "</cac:TaxCategory>" +
                              "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +
                            "<cac:Item>" +
                              "<cbc:Description><![CDATA[" + new Util().LimpiarCaracteresInvalidos(item.DescripcionProducto) + "]]></cbc:Description>" +
                              "<cac:SellersItemIdentification>" +
                                "<cbc:ID>" + item.CodigoProducto + "</cbc:ID>" +
                              "</cac:SellersItemIdentification>" +
                    //(item.CodigoSunat != String.Empty ?
                    //"<cac:CommodityClassification>" +
                    //"<cbc:ItemClassificationCode>" + item.CodigoSunat + "</cbc:ItemClassificationCode>" +
                    //"</cac:CommodityClassification>" : String.Empty) +
                            "</cac:Item>" +
                            "<cac:Price>" +
                              //"<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorUnitarioxItem.ToString("#0.00") + "</cbc:PriceAmount>" +
                              "<cbc:PriceAmount currencyID=\"" + item.CodigoTipoMoneda + "\">" + item.ValorVentaxItem.ToString("#0.00") + "</cbc:PriceAmount>" +
                            "</cac:Price>" +
                        "</cac:DebitNoteLine>";
                intLinea++;
            }

            __xml = __xml + detalle;

            __xml = __xml + "</DebitNote>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        #endregion

        #region Enviar Comunicacion Baja

        public EResultado EnviarComunicacionBajaOSE(EComunicacionBaja comunicacionbaja)
        {
            string strEnviarCB = ConfigurationManager.AppSettings["EnviarComunicacionBaja"];
            //String fechageneraciontmp = Convert.ToDateTime(comunicacionbaja.FechaGeneracionDocumento).ToString("yyyyMMdd");
            String fechageneraciontmp = Convert.ToDateTime(comunicacionbaja.FechaEmisionDocumentos).ToString("yyyyMMdd");
            comunicacionbaja.IdentificadorResumen = "RA-" + fechageneraciontmp + "-1";

            comunicacionbaja.RUCEmpresaEmisora = ConfigurationManager.AppSettings["RUC_EDITORA"];
            comunicacionbaja.TipoDocumentoEmpresaEmisora = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];
            comunicacionbaja.NombreComercialEmpresaEmisora = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];

            StringBuilder _xml = ArmarXmlComunicacionBaja(comunicacionbaja);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Registrar Solicitud
            //Int64 idsolicitud = FabricaSingleton<DComprobante>.Crear().RegistrarSolicitudResumenes("Comunicacion Baja"
            //                                                                            , comunicacionbaja.IdentificadorResumen
            //                                                                            , Convert.ToDateTime(comunicacionbaja.FechaEmisionDocumentos)
            //                                                                            , DateTime.Now
            //                                                                            , comunicacionbaja.RUCEmpresaEmisora
            //                                                                            , comunicacionbaja.TipoDocumentoEmpresaEmisora
            //                                                                            , comunicacionbaja.NombreComercialEmpresaEmisora);

            //foreach (EDetalleComunicacionBaja det in comunicacionbaja.ListadoComprobantes)
            //{
            //    FabricaSingleton<DComprobante>.Crear().RegistrarDetalleSolicitudResumen(idsolicitud
            //                                                                          , Convert.ToInt32(det.Item)
            //                                                                          , det.TipoDocumento
            //                                                                          , det.SerieDocumento
            //                                                                          , det.NroCorrelativoDocumento
            //                                                                          , String.Empty
            //                                                                          , String.Empty
            //                                                                           , det.MotivoBaja
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0
            //                                                                          , (Decimal)0);
            //}


            String filenameXml = comunicacionbaja.RUCEmpresaEmisora + "-RA-" + fechageneraciontmp + "-1.xml";
            String filenameZip = comunicacionbaja.RUCEmpresaEmisora + "-RA-" + fechageneraciontmp + "-1.zip";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            //SETEAR POR FECHA DE DOCUMENTO
            path += @"\" + fechageneraciontmp.Substring(0, 4) + @"\" + fechageneraciontmp.Substring(4, 2) + @"\" + fechageneraciontmp.Substring(6, 2);

            String tmpXml = path + "/" + filenameXml;
            filenameZip = path + "/" + filenameZip;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Firmar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXml(tmpXml, "VoidedDocuments", ref SignatureValue, ref DigestValue);

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (strEnviarCB == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;
                    //TLS1.2
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                    //OSE
                    ClientCredentials clientCredentials = new ClientCredentials();
                    clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                    clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                    var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                    proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                    proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat, "");
                    proxy.Close();

                    eresult.CDR = "0";
                    eresult.EstadoComprobante = "DE BAJA";
                    eresult.MensajeResultadoSunat = "Se genero el Número Ticket : " + numeroticket;
                }
                else
                {
                    eresult.CDR = "0";
                }
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = String.Empty;
                eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlComunicacionBaja(EComunicacionBaja comunicacionbaja)
        {
            EstructuraXMLComunicacionBaja estructura = new EstructuraXMLComunicacionBaja();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<VoidedDocuments xmlns=\"urn:sunat:names:specification:ubl:peru:schema:xsd:VoidedDocuments-1\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                //"<cbc:UBLVersionID>2.1</cbc:UBLVersionID>" +
                //"<cbc:CustomizationID>2.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + comunicacionbaja.IdentificadorResumen + "</cbc:ID>" +
                                "<cbc:ReferenceDate>" + comunicacionbaja.FechaGeneracionDocumento + "</cbc:ReferenceDate>" +
                                "<cbc:IssueDate>" + comunicacionbaja.FechaEmisionDocumentos + "</cbc:IssueDate>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + comunicacionbaja.RUCEmpresaEmisora + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(comunicacionbaja.NombreComercialEmpresaEmisora) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + comunicacionbaja.RUCEmpresaEmisora + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + comunicacionbaja.TipoDocumentoEmpresaEmisora + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(comunicacionbaja.NombreComercialEmpresaEmisora) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>";

            String detalle = "";

            foreach (EDetalleComunicacionBaja item in comunicacionbaja.ListadoComprobantes)
            {
                detalle = detalle + "<sac:VoidedDocumentsLine>" +
                            "<cbc:LineID>" + item.Item + "</cbc:LineID>" +
                            "<cbc:DocumentTypeCode>" + item.TipoDocumento + "</cbc:DocumentTypeCode>" +
                            "<sac:DocumentSerialID>" + item.SerieDocumento + "</sac:DocumentSerialID>" +
                            "<sac:DocumentNumberID>" + item.NroCorrelativoDocumento + "</sac:DocumentNumberID>" +
                            "<sac:VoidReasonDescription>" + new Util().LimpiarCaracteresInvalidos(item.MotivoBaja) + "</sac:VoidReasonDescription>" +
                          "</sac:VoidedDocumentsLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</VoidedDocuments>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }

        #endregion

        #region Enviar Resumen Diario

        public EResultado EnviarResumenDiario(EResumenDiario resumendiario)
        {
            string strEnviarSunat = ConfigurationManager.AppSettings["EnviarResumenDiario"];
            String fechageneraciontmp = Convert.ToDateTime(resumendiario.FechaGeneracionResumen).ToString("yyyyMMdd");
            //resumendiario.IdentificadorResumen = "RC-" + fechageneraciontmp + "-1";

            resumendiario.RUCEmpresaEmisora = ConfigurationManager.AppSettings["RUC_EDITORA"];
            resumendiario.TipoDocumentoEmpresaEmisora = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];
            resumendiario.NombreComercialEmpresaEmisora = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];

            StringBuilder _xml = ArmarXmlResumenDiario(resumendiario);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Registrar Solicitud
            //Int64 idsolicitud = FabricaSingleton<DComprobante>.Crear().RegistrarSolicitudResumenes("Resumen Diario"
            //                                                                            , resumendiario.IdentificadorResumen
            //                                                                            , Convert.ToDateTime(resumendiario.FechaEmisionDocumentos)
            //                                                                            , DateTime.Now
            //                                                                            , resumendiario.RUCEmpresaEmisora
            //                                                                            , resumendiario.TipoDocumentoEmpresaEmisora
            //                                                                            , resumendiario.NombreComercialEmpresaEmisora);

            //foreach (EDetalleResumenDiario det in resumendiario.ListadoResumenDiario)
            //{
            //    FabricaSingleton<DComprobante>.Crear().RegistrarDetalleSolicitudResumen(idsolicitud
            //                                                                          , Convert.ToInt32(det.Item)
            //                                                                          , det.TipoDocumento
            //                                                                          , det.SerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.InicioSerieDocumento
            //                                                                          , det.FinSerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.ImporteTotalVenta
            //                                                                          , det.ImporteTotalVenta_OG
            //                                                                          , det.ImporteTotalVenta_OE
            //                                                                          , det.ImporteTotalVenta_OI
            //                                                                          , det.ImporteTotalOtrosCargos
            //                                                                          , det.ImporteTotalISC
            //                                                                          , det.ImporteTotalIGV
            //                                                                          , det.ImporteTotalOtrosTributos);
            //}

            //Guardar xml
            String filenameXml = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.xml";
            String filenameZip = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.zip";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            path += @"\" + fechageneraciontmp.Substring(0, 4) + @"\" + fechageneraciontmp.Substring(4, 2) + @"\" + fechageneraciontmp.Substring(6, 2);

            String tmpXml = path + @"\" + filenameXml;
            filenameZip = path + @"\" + filenameZip;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Firmar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXml(tmpXml, "SummaryDocuments", ref SignatureValue, ref DigestValue);

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (strEnviarSunat == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;

                    //OSE
                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");
                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat, "");
                    proxy.Close();

                    eresult.CDR = "0";
                    eresult.MensajeResultadoSunat = numeroticket;
                }
                else
                {
                    eresult.CDR = "0";
                }

                //eresult.EstadoComprobante = "DECLARADO";
                //eresult.MensajeResultadoSunat = "Se genero el Número Ticket : " + numeroticket;

                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.MensajeResultadoSunat = mensajesunat;

                //eresult.EstadoComprobante = String.Empty;
                //eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }

            return eresult;
        }
        public EResultado EnviarResumenDiario2018(EResumenDiario resumendiario)
        {
            string strEnviarSunat = ConfigurationManager.AppSettings["EnviarResumenDiario"];
            String fechaEmisionDocumento = Convert.ToDateTime(resumendiario.FechaEmisionDocumentos).ToString("yyyyMMdd");
            String fechageneraciontmp = Convert.ToDateTime(resumendiario.FechaGeneracionResumen).ToString("yyyyMMdd");

            resumendiario.IdentificadorResumen = "RC-" + fechageneraciontmp + "-1";

            //resumendiario.IdentificadorResumen = "RC-" + fechaEmisionDocumento + "-1";

            resumendiario.RUCEmpresaEmisora = ConfigurationManager.AppSettings["RUC_EDITORA"];
            resumendiario.TipoDocumentoEmpresaEmisora = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];
            resumendiario.NombreComercialEmpresaEmisora = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];

            StringBuilder _xml = ArmarXmlResumenDiario2018(resumendiario);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Registrar Solicitud
            //Int64 idsolicitud = FabricaSingleton<DComprobante>.Crear().RegistrarSolicitudResumenes("Resumen Diario"
            //                                                                            , resumendiario.IdentificadorResumen
            //                                                                            , Convert.ToDateTime(resumendiario.FechaEmisionDocumentos)
            //                                                                            , DateTime.Now
            //                                                                            , resumendiario.RUCEmpresaEmisora
            //                                                                            , resumendiario.TipoDocumentoEmpresaEmisora
            //                                                                            , resumendiario.NombreComercialEmpresaEmisora);

            //foreach (EDetalleResumenDiario det in resumendiario.ListadoResumenDiario)
            //{
            //    FabricaSingleton<DComprobante>.Crear().RegistrarDetalleSolicitudResumen(idsolicitud
            //                                                                          , Convert.ToInt32(det.Item)
            //                                                                          , det.TipoDocumento
            //                                                                          , det.SerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.InicioSerieDocumento
            //                                                                          , det.FinSerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.ImporteTotalVenta
            //                                                                          , det.ImporteTotalVenta_OG
            //                                                                          , det.ImporteTotalVenta_OE
            //                                                                          , det.ImporteTotalVenta_OI
            //                                                                          , det.ImporteTotalOtrosCargos
            //                                                                          , det.ImporteTotalISC
            //                                                                          , det.ImporteTotalIGV
            //                                                                          , det.ImporteTotalOtrosTributos);
            //}

            //Guardar xml
            String filenameXml = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.xml";
            String filenameZip = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.zip";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            path += @"\" + fechageneraciontmp.Substring(0, 4) + @"\" + fechageneraciontmp.Substring(4, 2) + @"\" + fechageneraciontmp.Substring(6, 2);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            String tmpXml = path + @"\" + filenameXml;
            filenameZip = path + @"\" + filenameZip;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Firmar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXml(tmpXml, "SummaryDocuments", ref SignatureValue, ref DigestValue);

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (strEnviarSunat == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;

                    //OSE
                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");
                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat, "");
                    proxy.Close();

                    eresult.CDR = "0";
                    eresult.MensajeResultadoSunat = numeroticket;
                }
                else
                {
                    eresult.CDR = "0";
                }

                //eresult.EstadoComprobante = "DECLARADO";
                //eresult.MensajeResultadoSunat = "Se genero el Número Ticket : " + numeroticket;

                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.MensajeResultadoSunat = mensajesunat;

                //eresult.EstadoComprobante = String.Empty;
                //eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }

            return eresult;
        }
        public EResultado EnviarResumenDiarioMasivo(EResumenDiario resumendiario, string correlaFile)
        {
            string strEnviarSunat = ConfigurationManager.AppSettings["EnviarResumenDiario"];
            String fechaEmisionDocumento = Convert.ToDateTime(resumendiario.FechaEmisionDocumentos).ToString("yyyyMMdd");
            String fechageneraciontmp = Convert.ToDateTime(resumendiario.FechaGeneracionResumen).ToString("yyyyMMdd");

            //resumendiario.IdentificadorResumen = "RC-" + fechageneraciontmp + "-1";
            resumendiario.IdentificadorResumen = "RC-" + fechageneraciontmp + correlaFile;


            resumendiario.RUCEmpresaEmisora = ConfigurationManager.AppSettings["RUC_EDITORA"];
            resumendiario.TipoDocumentoEmpresaEmisora = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];
            resumendiario.NombreComercialEmpresaEmisora = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];

            StringBuilder _xml = ArmarXmlResumenDiario2018(resumendiario);

            //Envio a Sunat
            byte[] xmlsunat = new Util().GenerarXml(_xml);

            //Registrar Solicitud
            //Int64 idsolicitud = FabricaSingleton<DComprobante>.Crear().RegistrarSolicitudResumenes("Resumen Diario"
            //                                                                            , resumendiario.IdentificadorResumen
            //                                                                            , Convert.ToDateTime(resumendiario.FechaEmisionDocumentos)
            //                                                                            , DateTime.Now
            //                                                                            , resumendiario.RUCEmpresaEmisora
            //                                                                            , resumendiario.TipoDocumentoEmpresaEmisora
            //                                                                            , resumendiario.NombreComercialEmpresaEmisora);

            //foreach (EDetalleResumenDiario det in resumendiario.ListadoResumenDiario)
            //{
            //    FabricaSingleton<DComprobante>.Crear().RegistrarDetalleSolicitudResumen(idsolicitud
            //                                                                          , Convert.ToInt32(det.Item)
            //                                                                          , det.TipoDocumento
            //                                                                          , det.SerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.InicioSerieDocumento
            //                                                                          , det.FinSerieDocumento
            //                                                                          , String.Empty
            //                                                                          , det.ImporteTotalVenta
            //                                                                          , det.ImporteTotalVenta_OG
            //                                                                          , det.ImporteTotalVenta_OE
            //                                                                          , det.ImporteTotalVenta_OI
            //                                                                          , det.ImporteTotalOtrosCargos
            //                                                                          , det.ImporteTotalISC
            //                                                                          , det.ImporteTotalIGV
            //                                                                          , det.ImporteTotalOtrosTributos);
            //}

            //Guardar xml
            //String filenameXml = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.xml";
            //String filenameZip = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + "-1.zip";

            String filenameXml = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + correlaFile + ".xml";
            String filenameZip = resumendiario.RUCEmpresaEmisora + "-RC-" + fechageneraciontmp + correlaFile + ".zip";

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            path += @"\" + fechageneraciontmp.Substring(0, 4) + @"\" + fechageneraciontmp.Substring(4, 2) + @"\" + fechageneraciontmp.Substring(6, 2);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            String tmpXml = path + @"\" + filenameXml;
            filenameZip = path + @"\" + filenameZip;

            if (File.Exists(tmpXml))
                File.Delete(tmpXml);

            //Guardar xml
            new Util().SaveData(xmlsunat, tmpXml);

            //Firmar Xml
            String SignatureValue = "";
            String DigestValue = "";
            new FirmaDigital().FirmarXml(tmpXml, "SummaryDocuments", ref SignatureValue, ref DigestValue);

            //Comprimir Xml
            if (File.Exists(filenameZip))
                File.Delete(filenameZip);

            Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

            EResultado eresult = new EResultado();

            try
            {
                if (strEnviarSunat == "SI")
                {
                    byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                    ServicePointManager.Expect100Continue = false;

                    //OSE
                    IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");
                    proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                    String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat,"");
                    proxy.Close();

                    eresult.CDR = "0";
                    eresult.MensajeResultadoSunat = numeroticket;
                }
                else
                {
                    eresult.CDR = "0";
                }

                //eresult.EstadoComprobante = "DECLARADO";
                //eresult.MensajeResultadoSunat = "Se genero el Número Ticket : " + numeroticket;

                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.MensajeResultadoSunat = mensajesunat;

                //eresult.EstadoComprobante = String.Empty;
                //eresult.MensajeResultadoSunat = mensajesunat;
                //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
            }

            return eresult;
        }

        private StringBuilder ArmarXmlResumenDiario(EResumenDiario resumendiario)
        {
            EstructuraXMLResumenDiario estructura = new EstructuraXMLResumenDiario();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            //Escribir_Log("11");

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<SummaryDocuments xmlns=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SummaryDocuments-1\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"urn:sunat:names:specification:ubl:peru:schema:xsd:InvoiceSummary-1 D:\\UBL_SUNAT\\SUNAT_xml_20110112\\20110112\\xsd\\maindoc\\UBLPE-InvoiceSummary-1.0.xsd\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + resumendiario.IdentificadorResumen + "</cbc:ID>" +
                                "<cbc:ReferenceDate>" + resumendiario.FechaGeneracionResumen + "</cbc:ReferenceDate>" +
                                "<cbc:IssueDate>" + resumendiario.FechaEmisionDocumentos + "</cbc:IssueDate>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + resumendiario.RUCEmpresaEmisora + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(resumendiario.NombreComercialEmpresaEmisora) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + resumendiario.RUCEmpresaEmisora + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + resumendiario.TipoDocumentoEmpresaEmisora + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(resumendiario.NombreComercialEmpresaEmisora) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>";

            String detalle = "";

            //Escribir_Log("22");

            foreach (EDetalleResumenDiario item in resumendiario.ListadoResumenDiario)
            {
                detalle = detalle + "<sac:SummaryDocumentsLine>" +
                            "<cbc:LineID>" + item.Item + "</cbc:LineID>" +
                            "<cbc:DocumentTypeCode>" + item.TipoDocumento + "</cbc:DocumentTypeCode>" +
                            "<sac:DocumentSerialID>" + item.SerieDocumento + "</sac:DocumentSerialID>" +
                            "<sac:StartDocumentNumberID>" + item.InicioSerieDocumento + "</sac:StartDocumentNumberID>" +
                            "<sac:EndDocumentNumberID>" + item.FinSerieDocumento + "</sac:EndDocumentNumberID>" +
                            "<sac:TotalAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta.ToString("#0.00") + "</sac:TotalAmount>" +
                            "<sac:BillingPayment>" +
                            "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OG.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>01</cbc:InstructionID>" +
                            "</sac:BillingPayment>" +
                            "<sac:BillingPayment>" +
                            "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OE.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>02</cbc:InstructionID>" +
                            "</sac:BillingPayment>" +
                            "<sac:BillingPayment>" +
                            "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OI.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>03</cbc:InstructionID>" +
                            "</sac:BillingPayment>" +
                            "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>true</cbc:ChargeIndicator>" +
                                "<cbc:Amount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosCargos.ToString("#0.00") + "</cbc:Amount>" +
                            "</cac:AllowanceCharge>" +

                            "<cac:TaxTotal>" +
                            "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalISC.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalISC.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>2000</cbc:ID>" +
                                            "<cbc:Name>ISC</cbc:Name>" +
                                            "<cbc:TaxTypeCode>EXC</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                                "</cac:TaxTotal>" +

                            "<cac:TaxTotal>" +
                            "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalIGV.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalIGV.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>1000</cbc:ID>" +
                                            "<cbc:Name>IGV</cbc:Name>" +
                                            "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +

                            (item.ImporteTotalOtrosTributos > 0 ?
                            "<cac:TaxTotal>" +
                            "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosTributos.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosTributos.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>9999</cbc:ID>" +
                                            "<cbc:Name>OTROS</cbc:Name>" +
                                            "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                                "</cac:TaxTotal>" : "") +

                          "</sac:SummaryDocumentsLine>";

                //Escribir_Log(item.Item.ToString());
            }

            //Escribir_Log("33");

            __xml = __xml + detalle;

            __xml = __xml + "</SummaryDocuments>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }
        private StringBuilder ArmarXmlResumenDiario2018(EResumenDiario resumendiario)
        {
            EstructuraXMLResumenDiario estructura = new EstructuraXMLResumenDiario();
            StringBuilder _xml = new StringBuilder();
            String encoding = ConfigurationManager.AppSettings["Encoding"];

            //Escribir_Log("11");

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<SummaryDocuments xmlns=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SummaryDocuments-1\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"urn:sunat:names:specification:ubl:peru:schema:xsd:InvoiceSummary-1 D:\\UBL_SUNAT\\SUNAT_xml_20110112\\20110112\\xsd\\maindoc\\UBLPE-InvoiceSummary-1.0.xsd\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.1</cbc:CustomizationID>" +
                                "<cbc:ID>" + resumendiario.IdentificadorResumen + "</cbc:ID>" +
                                "<cbc:ReferenceDate>" + resumendiario.FechaEmisionDocumentos + "</cbc:ReferenceDate>" +
                                "<cbc:IssueDate>" + resumendiario.FechaGeneracionResumen + "</cbc:IssueDate>" +

                                // "<cbc:ID>RC20150805</cbc:ID>" +
                //"<cbc:ReferenceDate>2017-12-31</cbc:ReferenceDate>" +
                //"<cbc:IssueDate>2017-12-31</cbc:IssueDate>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + resumendiario.RUCEmpresaEmisora + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(resumendiario.NombreComercialEmpresaEmisora) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + resumendiario.RUCEmpresaEmisora + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>" + resumendiario.TipoDocumentoEmpresaEmisora + "</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(resumendiario.NombreComercialEmpresaEmisora) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>";

            String detalle = "";

            //Escribir_Log("22");

            foreach (EDetalleResumenDiario item in resumendiario.ListadoResumenDiario)
            {
                detalle = detalle + "<sac:SummaryDocumentsLine>" +
                            "<cbc:LineID>" + item.Item + "</cbc:LineID>" +
                            "<cbc:DocumentTypeCode>" + item.TipoDocumento + "</cbc:DocumentTypeCode>" +
                            "<cbc:ID>" + item.SerieComprobante + "-" + item.CorrelativoComprobante + "</cbc:ID>" +

                            "<cac:AccountingCustomerParty>" +
                              "<cbc:CustomerAssignedAccountID>" + item.NumDocIdentidad + "</cbc:CustomerAssignedAccountID>" +
                              "<cbc:AdditionalAccountID>" + item.TipoDocIdentidad + "</cbc:AdditionalAccountID>" +
                            "</cac:AccountingCustomerParty>" +

                             (item.TipoComprobante22 != " " ?
                              "<cac:BillingReference>" +
                              "<cac:InvoiceDocumentReference>" +
                                 "<cbc:ID>" + item.SerieComprobante22 + "-" + item.CorrelativoComprobante22 + "</cbc:ID>" +
                                 "<cbc:DocumentTypeCode>" + item.TipoComprobante22 + "</cbc:DocumentTypeCode>" +
                               "</cac:InvoiceDocumentReference>" +
                            "</cac:BillingReference>" : String.Empty) +


                            //"<sac:SUNATPerceptionSummaryDocumentReference>" +
                    //     "<sac:SUNATPerceptionSystemCode>0</sac:SUNATPerceptionSystemCode>" +
                    //     "<sac:SUNATPerceptionSystemPercent>0</sac:SUNATPerceptionSystemPercent>" +
                    //     "<cbc:TotalInvoiceAmount>0</cbc:TotalInvoiceAmount>" +
                    //     "<sac:SUNATTotalCashed>0</sac:SUNATTotalCashed>" +
                    //     "<cbc:TaxableAmount>0</cbc:TaxableAmount>" +                                 
                    //"</sac:SUNATPerceptionSummaryDocumentReference>" +
                            "<cac:Status>" +
                              "<cbc:ConditionCode>" + item.EstadoItem + "</cbc:ConditionCode>" +
                            "</cac:Status>" +

                            "<sac:TotalAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta.ToString("#0.00") + "</sac:TotalAmount>" +

                            (item.ImporteTotalVenta_OG > 0 ?
                            "<sac:BillingPayment>" +
                                "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OG.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>01</cbc:InstructionID>" +
                            "</sac:BillingPayment>" : "") +
                            (item.ImporteTotalVenta_OE > 0 ?
                            "<sac:BillingPayment>" +
                                "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OE.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>02</cbc:InstructionID>" +
                            "</sac:BillingPayment>" : "") +

                            (item.ImporteTotalVenta_OI > 0 ?
                            "<sac:BillingPayment>" +
                                "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalVenta_OI.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>03</cbc:InstructionID>" +
                            "</sac:BillingPayment>" : "") +

                            (item.ImporteGratuito > 0 ?
                            "<sac:BillingPayment>" +
                                "<cbc:PaidAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteGratuito.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:InstructionID>04</cbc:InstructionID>" +
                            "</sac:BillingPayment>" : "") +

                            (item.ImporteTotalOtrosCargos > 0 ?
                            "<cac:AllowanceCharge>" +
                                "<cbc:ChargeIndicator>true</cbc:ChargeIndicator>" +
                                "<cbc:Amount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosCargos.ToString("#0.00") + "</cbc:Amount>" +
                            "</cac:AllowanceCharge>" : "") +

                            "<cac:TaxTotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalISC.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                    "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalISC.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>2000</cbc:ID>" +
                                            "<cbc:Name>ISC</cbc:Name>" +
                                            "<cbc:TaxTypeCode>EXC</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +

                            "<cac:TaxTotal>" +
                            "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalIGV.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalIGV.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>1000</cbc:ID>" +
                                            "<cbc:Name>IGV</cbc:Name>" +
                                            "<cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                            "</cac:TaxTotal>" +

                            (item.ImporteTotalOtrosTributos > 0 ?
                            "<cac:TaxTotal>" +
                            "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosTributos.ToString("#0.00") + "</cbc:TaxAmount>" +
                                "<cac:TaxSubtotal>" +
                                "<cbc:TaxAmount currencyID=\"" + item.TipoMoneda + "\">" + item.ImporteTotalOtrosTributos.ToString("#0.00") + "</cbc:TaxAmount>" +
                                    "<cac:TaxCategory>" +
                                        "<cac:TaxScheme>" +
                                            "<cbc:ID>9999</cbc:ID>" +
                                            "<cbc:Name>OTROS</cbc:Name>" +
                                            "<cbc:TaxTypeCode>OTH</cbc:TaxTypeCode>" +
                                        "</cac:TaxScheme>" +
                                    "</cac:TaxCategory>" +
                                "</cac:TaxSubtotal>" +
                                "</cac:TaxTotal>" : "") +

                          "</sac:SummaryDocumentsLine>";

                //Escribir_Log(item.Item.ToString());
            }

            //Escribir_Log("33");

            __xml = __xml + detalle;

            __xml = __xml + "</SummaryDocuments>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }
        #endregion

        #region Enviar Retencion a Sunat
        public EResultado EnviarRetencionOSE(ERetencion retencion)
        {
            EResultado eresult = new EResultado();

            string strEnviarRetencion = ConfigurationManager.AppSettings["EnviarRetencion"];

            //TIPO DE DOCUMENTO (20 RETENCIONES)
            retencion.TipoDocumento = "20";

            retencion.MensajeResolucionSunat = ConfigurationManager.AppSettings["MENSAJE_CONSULTA"]; //ADD MMILLA
            retencion.RUCEmisor = ConfigurationManager.AppSettings["RUC_EDITORA"];
            retencion.TipoDocumentoEmisor = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            retencion.NombreComercialEmisor = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];
            retencion.UbigeoEmisor = ConfigurationManager.AppSettings["UBIGEO_EDITORA"];
            retencion.UrbanizacionEmisor = ConfigurationManager.AppSettings["URBANIZACION_EDITORA"];
            retencion.DepartamentoEmisor = ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"];
            retencion.ProvinciaEmisor = ConfigurationManager.AppSettings["PROVINCIA_EDITORA"];
            retencion.DistritoEmisor = ConfigurationManager.AppSettings["DISTRITO_EDITORA"];
            retencion.CodigoPaisEmisor = ConfigurationManager.AppSettings["CODIGOPAIS_EDITORA"];
            retencion.RazonSocialEmisor = ConfigurationManager.AppSettings["RAZONSOCIAL_EDITORA"];
            retencion.DireccionEmisor = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim();
            retencion.DireccionEmisorCompleta = ConfigurationManager.AppSettings["DIRECCION_EDITORA"].Trim() + //", " + ConfigurationManager.AppSettings["URBANIZACION_EDITORA"].Trim() +
            ", " + ConfigurationManager.AppSettings["DISTRITO_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["PROVINCIA_EDITORA"].Trim() + ", " + ConfigurationManager.AppSettings["DEPARTAMENTO_EDITORA"].Trim();

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();

            retencion.TelefonoEmisor = ConfigurationManager.AppSettings["TELEFONO_RETENCION"];

            try
            {
                Boolean exito = true;

                StringBuilder _xml = ArmarXmlRetencion(retencion);

                //Envio a Sunat
                byte[] xmlsunat = new Util().GenerarXml(_xml);

                //Guardar xml
                string strNombreArchivo = retencion.RUCEmisor + "-" + retencion.TipoDocumento + "-" + retencion.Serie + "-" + retencion.Correlativo.ToString();
                String filenameXml = strNombreArchivo + ".xml";
                String filenameRptaXml = "R-" + filenameXml;

                String filenameZip = strNombreArchivo + ".zip";
                String filenameRptaZip = "R-" + filenameZip;

                String filenamePDF = strNombreArchivo + ".pdf";
                String filenamePDF_ = strNombreArchivo + "_.pdf";

                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                //SETEAR POR FECHA DE EMISION
                string strFechaEmision = retencion.FechaEmision.ToString("yyyy/MM/dd");
                path += @"\" + strFechaEmision.Substring(0, 4) + @"\" + strFechaEmision.Substring(5, 2) + @"\" + strFechaEmision.Substring(8, 2);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                String tmpXml = path + @"\" + filenameXml;

                filenameZip = path + @"\" + filenameZip;
                filenameRptaZip = path + @"\" + filenameRptaZip;
                filenameRptaXml = path + @"\" + filenameRptaXml;

                //Guardar Xml
                if (File.Exists(tmpXml))
                    File.Delete(tmpXml);

                new Util().SaveData(xmlsunat, tmpXml);

                //Fimar Xml
                String SignatureValue = "";
                String DigestValue = "";
                new FirmaDigital().FirmarXmlRetencion(tmpXml, "Retention", ref SignatureValue, ref DigestValue);

                retencion.SignatureValue = SignatureValue;
                retencion.DigestValue = DigestValue;

                //Comprimir Xml
                if (File.Exists(filenameZip))
                    File.Delete(filenameZip);

                Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

                try
                {
                    //GENERAR PDF
                    filenamePDF_ = path + @"\" + filenamePDF_;
                    filenamePDF = path + @"\" + filenamePDF;

                    if (File.Exists(filenamePDF))
                        File.Delete(filenamePDF);

                    if (File.Exists(filenamePDF_))
                        File.Delete(filenamePDF_);

                    //Generar PDF
                    FabricaSingleton<RetencionPDF>.Crear().GenerarPDF(filenamePDF_, retencion);
                    itextUtil.FirmarPDF(filenamePDF_, filenamePDF, _HuellaFirmante, false);

                    if (File.Exists(filenamePDF_))
                        File.Delete(filenamePDF_);


                    //ENVIAR A SUNAT
                    if (strEnviarRetencion == "SI")
                    {
                        byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                        ServicePointManager.Expect100Continue = false;
                        //TLS1.2
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                        //OSE
                        ClientCredentials clientCredentials = new ClientCredentials();
                        clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                        clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                        IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                        var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                        proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                        proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                        proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                        proxy.Close();

                        //ProxySunat.billServiceClient proxy = new ProxySunat.billServiceClient("BillServicePort");
                        //proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        ////proxy.InnerChannel.OperationTimeout = new TimeSpan(0, 0, 120);
                        //byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                        //proxy.Close();

                        if (File.Exists(filenameRptaZip))
                            File.Delete(filenameRptaZip);

                        if (File.Exists(filenameRptaXml))
                            File.Delete(filenameRptaXml);

                        new Util().SaveData(oSalida, filenameRptaZip);

                        using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameRptaZip))
                        {
                            zip.ExtractAll(path);
                        }

                        //Leer Xml Respuesta Sunat
                        XmlDocument xmlDocRptaSunat = new XmlDocument();
                        xmlDocRptaSunat.Load(filenameRptaXml);

                        XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                        XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                        XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                        string responsecode = String.Empty, description = String.Empty;

                        foreach (XmlNode nodo in lista)
                        {
                            responsecode = nodo["cbc:ResponseCode"].InnerText;
                            description = nodo["cbc:Description"].InnerText;
                        }

                        String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                        eresult.CDR = responsecode;
                        eresult.EstadoComprobante = EstadoComprobante;
                        eresult.MensajeResultadoSunat = description;
                    }
                    else
                    {
                        exito = true;
                        eresult.CDR = "0";
                        eresult.EstadoComprobante = "";
                        eresult.MensajeResultadoSunat = "Procesado";
                    }

                }
                catch (System.ServiceModel.FaultException ex)
                {
                    String mensajesunat = "";
                    System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Message);

                    if (dtmensaje.Rows.Count > 0)
                    {
                        mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                    }

                    eresult.CDR = ex.Message;
                    eresult.EstadoComprobante = "RECHAZADO";
                    eresult.MensajeResultadoSunat = mensajesunat;
                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);

                    exito = false;
                }
            }
            catch (Exception ex)
            {
                Escribir_Log(ex.Message);
            }
            return eresult;
        }

        public String PruebaRetencion()
        {
            String filenameZip = @"D:\AccuratechSoftware\sunatinterface\SunatFiles\2016\05\27\20100072751-20-R001-1.zip";
            //String filenameZip = @"D:\AccuratechSoftware\sunatinterface\SunatFiles\2016\05\06\20100072751-01-F001-848.zip";
            byte[] ZipSunat = File.ReadAllBytes(filenameZip);

            ServicePointManager.Expect100Continue = false;

            try
            {
                ProxySunat.billServiceClient proxy = new ProxySunat.billServiceClient("SunatRetenciones");
                proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                proxy.Close();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                return ex.ToString();
            }
            return "";
        }

        private StringBuilder ArmarXmlRetencion(ERetencion retencion)
        {
            EstructuraXMLFactura estructura = new EstructuraXMLFactura();

            StringBuilder _xml = new StringBuilder();

            String encoding = ConfigurationManager.AppSettings["EncodingRetencion"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                "<Retention xmlns=\"urn:sunat:names:specification:ubl:peru:schema:xsd:Retention-1\" " +
                "xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" " +
                "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" " +
                "xmlns:ccts=\"urn:un:unece:uncefact:documentation:2\" " +
                "xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" " +
                "xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" " +
                "xmlns:qdt=\"urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2\" " +
                "xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" " +
                "xmlns:udt=\"urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<ext:UBLExtensions>" +
                    "<ext:UBLExtension>" +
                        "<ext:ExtensionContent>" +
                        "</ext:ExtensionContent>" +
                    "</ext:UBLExtension>" +
                "</ext:UBLExtensions>" +
                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                //SIGNATURE-INICIO
              "<cac:Signature>" +
                "<cbc:ID>IDSignKG</cbc:ID>" +
                "<cac:SignatoryParty>" +
                  "<cac:PartyIdentification>" +
                    "<cbc:ID>" + retencion.RUCEmisor + "</cbc:ID>" +
                  "</cac:PartyIdentification>" +
                  "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.NombreComercialEmisor) + "]]></cbc:Name>" +
                  "</cac:PartyName>" +
                "</cac:SignatoryParty>" +
                "<cac:DigitalSignatureAttachment>" +
                  "<cac:ExternalReference>" +
                    "<cbc:URI>#SignatureKG</cbc:URI>" +
                  "</cac:ExternalReference>" +
                "</cac:DigitalSignatureAttachment>" +
              "</cac:Signature>" +
                //SIGNATURE-FIN
              "<cbc:ID>" + retencion.Serie + "-" + retencion.Correlativo.ToString() + "</cbc:ID>" +
              "<cbc:IssueDate>" + retencion.FechaEmision.ToString("yyyy-MM-dd") + "</cbc:IssueDate>" +

              //DATOS DEL EMISOR ELECTRONICO
              "<cac:AgentParty>" +
                "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"6\">" + retencion.RUCEmisor + "</cbc:ID>" +
                "</cac:PartyIdentification>" +
                "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.RazonSocialEmisor) + "]]></cbc:Name>" +
                "</cac:PartyName>" +
                "<cac:PostalAddress>" +
                    "<cbc:ID>" + retencion.UbigeoEmisor + "</cbc:ID>" +
                    "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.DireccionEmisor) + "]]></cbc:StreetName>" +
                //"<cbc:CitySubdivisionName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.UrbanizacionEmisor) + "]]></cbc:CitySubdivisionName>" +
                    "<cbc:CityName>" + retencion.DepartamentoEmisor + "</cbc:CityName>" +
                    "<cbc:CountrySubentity>" + retencion.ProvinciaEmisor + "</cbc:CountrySubentity>" +
                    "<cbc:District>" + retencion.DistritoEmisor + "</cbc:District>" +
                    "<cac:Country>" +
                        "<cbc:IdentificationCode>" + retencion.CodigoPaisEmisor + "</cbc:IdentificationCode>" +
                    "</cac:Country>" +
                "</cac:PostalAddress>" +
                "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.RazonSocialEmisor) + "]]></cbc:RegistrationName>" +
                "</cac:PartyLegalEntity>" +
              "</cac:AgentParty>" +

              //DATOS DEL PROVEEDOR
              "<cac:ReceiverParty>" +
                "<cac:PartyIdentification>" +
                    "<cbc:ID schemeID=\"6\">" + retencion.RUC +
                    "</cbc:ID>" +
                "</cac:PartyIdentification>" +
                "<cac:PartyName>" +
                    "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.RazonSocial) + "]]></cbc:Name>" +
                "</cac:PartyName>" +
                //"<cac:PostalAddress>" +
                //    "<cbc:ID>" + retencion.UbigeoEmisor + "</cbc:ID>" +
                //    "<cbc:StreetName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.Direccion_Proveedor) + "]]></cbc:StreetName>" +
                //    "<cbc:CitySubdivisionName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.Urbanizacion_Proveedor) + "]]></cbc:CitySubdivisionName>" +
                //    "<cbc:CityName>" + retencion.Departamento_Proveedor + "</cbc:CityName>" +
                //    "<cbc:CountrySubentity>" + retencion.Provincia_Proveedor + "</cbc:CountrySubentity>" +
                //    "<cbc:District>" + retencion.Distrito_Proveedor + "</cbc:District>" +
                //    "<cac:Country>" +
                //        "<cbc:IdentificationCode>" + retencion.Pais_Proveedor + "</cbc:IdentificationCode>" +
                //    "</cac:Country>" +
                //"</cac:PostalAddress>" +
                "<cac:PartyLegalEntity>" +
                    "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(retencion.RazonSocial) + "]]></cbc:RegistrationName>" +
                "</cac:PartyLegalEntity>" +
              "</cac:ReceiverParty>" +

              //REGIMEN Y TASA
              "<sac:SUNATRetentionSystemCode>01</sac:SUNATRetentionSystemCode>" +
              "<sac:SUNATRetentionPercent>3.00</sac:SUNATRetentionPercent>" +
                //OBSERVACIONES (OPCIONAL)
                //"<cbc:Note></cbc:Note>"+
                //IMPORTE TOTAL RETENIDO CON MONEDA
              "<cbc:TotalInvoiceAmount currencyID=\"" + retencion.MonedaTotalPagado + "\">" + retencion.ImporteTotalRetenido.ToString("#0.00") + "</cbc:TotalInvoiceAmount>" +
                //IMPORTE TOTAL PAGADO con MONEDA
              "<sac:SUNATTotalPaid currencyID=\"" + retencion.MonedaTotalPagado + "\">" + retencion.ImporteTotalPagado + "</sac:SUNATTotalPaid>";

            String detalle = "";

            foreach (ERetencionDetalle item in retencion.DetalleRetencion)
            {
                detalle = detalle +
                        "<sac:SUNATRetentionDocumentReference>" +
                            "<cbc:ID schemeID=\"" + item.TipoDocumento + "\">" + item.NumeroComprobantePago + "</cbc:ID>" +
                            "<cbc:IssueDate>" + item.FechaEmisionDocumento.ToString("yyyy-MM-dd") + "</cbc:IssueDate>" +
                            "<cbc:TotalInvoiceAmount currencyID=\"" + item.MonedaDocumento + "\">" + item.ImporteTotalDocumento.ToString("#0.00") + "</cbc:TotalInvoiceAmount>" +
                    //DATO DEL PAGO
                            "<cac:Payment>" +
                                "<cbc:ID>" + item.TipoDocumentoPago + "</cbc:ID>" +
                                "<cbc:PaidAmount currencyID=\"" + item.MonedaPago + "\">" + item.ImportePagoSinRetencion.ToString("#0.00") + "</cbc:PaidAmount>" +
                                "<cbc:PaidDate>" + item.FechaPagoDocumento.ToString("yyyy-MM-dd") + "</cbc:PaidDate>" +
                            "</cac:Payment>" +
                    //DATOS DE LA RETENCION
                            "<sac:SUNATRetentionInformation>" +
                                "<sac:SUNATRetentionAmount currencyID=\"" + item.MonedaImporteRetenido + "\">" + item.ImporteRetenido.ToString("#0.00") + "</sac:SUNATRetentionAmount>" +
                                "<sac:SUNATRetentionDate>" + item.FechaRetencion.ToString("yyyy-MM-dd") + "</sac:SUNATRetentionDate>" +
                                "<sac:SUNATNetTotalPaid currencyID=\"" + item.MonedaTotalPagado + "\">" + item.ImporteTotalPagar.ToString("#0.00") + "</sac:SUNATNetTotalPaid>" +
                                (item.MonedaDocumento == "PEN" ? "<cac:ExchangeRate>" +
                                    "<cbc:SourceCurrencyCode>PEN</cbc:SourceCurrencyCode>" +
                                    "<cbc:TargetCurrencyCode>PEN</cbc:TargetCurrencyCode>" +
                                    "<cbc:CalculationRate>1.00</cbc:CalculationRate>" +
                                    "<cbc:Date>" + item.FechaPagoDocumento.ToString("yyyy-MM-dd") + "</cbc:Date>" +
                                "</cac:ExchangeRate>" :
                                "<cac:ExchangeRate>" +
                                    "<cbc:SourceCurrencyCode>USD</cbc:SourceCurrencyCode>" +
                                    "<cbc:TargetCurrencyCode>PEN</cbc:TargetCurrencyCode>" +
                                    "<cbc:CalculationRate>" + item.FactorTipoCambio + "</cbc:CalculationRate>" +
                                    "<cbc:Date>" + item.FechaPagoDocumento.ToString("yyyy-MM-dd") + "</cbc:Date>" +
                                "</cac:ExchangeRate>") +
                            "</sac:SUNATRetentionInformation>" +
                        "</sac:SUNATRetentionDocumentReference>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</Retention>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }
        #endregion

        #region Enviar Reversion
        public EResultado EnviarReversionOSE(EReversion reversion)
        {
            EResultado eresult = new EResultado();

            string strEnviarReversion = ConfigurationManager.AppSettings["EnviarReversion"];

            reversion.RUC = ConfigurationManager.AppSettings["RUC_EDITORA"];
            reversion.TipoDocumento = ConfigurationManager.AppSettings["TIPODOCUMENTO_EDITORA"];//6:Registro Unico Contribuyentes
            reversion.RazonSocial = ConfigurationManager.AppSettings["NOMBRECOMERCIAL_EDITORA"];

            _HuellaFirmante = ConfigurationManager.AppSettings["HuellaFirmante"].Trim();

            string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

            try
            {
                StringBuilder _xml = ArmarXmlReversion(reversion);

                //Envio a Sunat
                byte[] xmlsunat = new Util().GenerarXml(_xml);

                //SETEAR POR FECHA DE EMISION
                string strFechaEmision = reversion.FechaComunicacion.ToString("yyyy/MM/dd");
                path += @"\" + strFechaEmision.Substring(0, 4) + @"\" + strFechaEmision.Substring(5, 2) + @"\" + strFechaEmision.Substring(8, 2);

                //Guardar xml
                string strNombreArchivo = reversion.RUC + "-RR-" + reversion.FechaComunicacion.ToString("yyyyMMdd") + "-1";
                String filenameXml = strNombreArchivo + ".xml";
                String filenameRptaXml = "R-" + filenameXml;

                String filenameZip = strNombreArchivo + ".zip";
                String filenameRptaZip = "R-" + filenameZip;

                String filenamePDF = strNombreArchivo + ".pdf";
                String filenamePDF_ = strNombreArchivo + "_.pdf";

                String tmpXml = path + @"\" + filenameXml;

                filenameZip = path + @"\" + filenameZip;
                filenameRptaZip = path + @"\" + filenameRptaZip;
                filenameRptaXml = path + @"\" + filenameRptaXml;

                //Guardar Xml
                if (File.Exists(tmpXml))
                    File.Delete(tmpXml);

                new Util().SaveData(xmlsunat, tmpXml);

                //Fimar Xml
                String SignatureValue = "";
                String DigestValue = "";
                new FirmaDigital().FirmarXmlReversion(tmpXml, "VoidedDocuments", ref SignatureValue, ref DigestValue);

                reversion.SignatureValue = SignatureValue;
                reversion.DigestValue = DigestValue;

                //Comprimir Xml
                if (File.Exists(filenameZip))
                    File.Delete(filenameZip);

                Boolean comprimido = new Util().ComprimirArchivo(tmpXml, filenameZip);

                try
                {
                    if (strEnviarReversion == "SI")
                    {
                        byte[] ZipSunat = File.ReadAllBytes(filenameZip);

                        ServicePointManager.Expect100Continue = false;
                        //TLS1.2
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                        //OSE
                        ClientCredentials clientCredentials = new ClientCredentials();
                        clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                        clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                        IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                        var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                        proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                        proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                        proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        //byte[] oSalida = proxy.sendBill(Path.GetFileName(filenameZip), ZipSunat);
                        String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat, "");
                        proxy.Close();

                        //ProxySunat.billServiceClient proxy = new ProxySunat.billServiceClient("BillServicePort");
                        //proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                        //String numeroticket = proxy.sendSummary(Path.GetFileName(filenameZip), ZipSunat);
                        //proxy.Close();

                        eresult.CDR = "0";
                        eresult.EstadoComprobante = "DE BAJA";
                        eresult.MensajeResultadoSunat = "Se generó el Número Ticket : " + numeroticket;
                    }
                    else
                    {
                        eresult.CDR = "0";
                    }
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    String mensajesunat = "";
                    System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                    if (dtmensaje.Rows.Count > 0)
                    {
                        mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                    }

                    eresult.CDR = ex.Code.Name;
                    eresult.EstadoComprobante = String.Empty;
                    eresult.MensajeResultadoSunat = mensajesunat;
                    //FabricaSingleton<DComprobante>.Crear().RegistrarRespuestaSunat(idsolicitud, eresult);
                }

            }
            catch (Exception ex)
            {
                Escribir_Log(ex.Message);
            }

            return eresult;
        }
        #endregion

        #region Armar XML Reversion
        private StringBuilder ArmarXmlReversion(EReversion reversion)
        {
            EstructuraXMLFactura estructura = new EstructuraXMLFactura();

            StringBuilder _xml = new StringBuilder();

            String encoding = ConfigurationManager.AppSettings["EncodingRetencion"];

            String __xml = "<?xml version=\"1.0\" encoding=\"" + encoding + "\" standalone=\"no\"?>" +
                           "<VoidedDocuments xmlns=\"urn:sunat:names:specification:ubl:peru:schema:xsd:VoidedDocuments-1\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\" xmlns:sac=\"urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                                "<ext:UBLExtensions>" +
                                    "<ext:UBLExtension>" +
                                        "<ext:ExtensionContent>" +
                                        "</ext:ExtensionContent>" +
                                    "</ext:UBLExtension>" +
                                "</ext:UBLExtensions>" +
                                "<cbc:UBLVersionID>2.0</cbc:UBLVersionID>" +
                                "<cbc:CustomizationID>1.0</cbc:CustomizationID>" +
                                "<cbc:ID>" + "RR-" + reversion.FechaComunicacion.ToString("yyyyMMdd") + "-1" + "</cbc:ID>" +
                                "<cbc:ReferenceDate>" + reversion.FechaEmision.ToString("yyyy-MM-dd") + "</cbc:ReferenceDate>" +
                                "<cbc:IssueDate>" + reversion.FechaComunicacion.ToString("yyyy-MM-dd") + "</cbc:IssueDate>" +

                                "<cac:Signature>" +
                                    "<cbc:ID>IDSignKG</cbc:ID>" +
                                    "<cac:SignatoryParty>" +
                                      "<cac:PartyIdentification>" +
                                        "<cbc:ID>" + reversion.RUC + "</cbc:ID>" +
                                      "</cac:PartyIdentification>" +
                                      "<cac:PartyName>" +
                                        "<cbc:Name><![CDATA[" + new Util().LimpiarCaracteresInvalidos(reversion.RazonSocial) + "]]></cbc:Name>" +
                                      "</cac:PartyName>" +
                                    "</cac:SignatoryParty>" +
                                    "<cac:DigitalSignatureAttachment>" +
                                      "<cac:ExternalReference>" +
                                        "<cbc:URI>signatureKG</cbc:URI>" +
                                      "</cac:ExternalReference>" +
                                    "</cac:DigitalSignatureAttachment>" +
                                "</cac:Signature>" +

                                "<cac:AccountingSupplierParty>" +
                                    "<cbc:CustomerAssignedAccountID>" + reversion.RUC + "</cbc:CustomerAssignedAccountID>" +
                                    "<cbc:AdditionalAccountID>6</cbc:AdditionalAccountID>" +
                                    "<cac:Party>" +
                                      "<cac:PartyLegalEntity>" +
                                        "<cbc:RegistrationName><![CDATA[" + new Util().LimpiarCaracteresInvalidos(reversion.RazonSocial) + "]]></cbc:RegistrationName>" +
                                      "</cac:PartyLegalEntity>" +
                                    "</cac:Party>" +
                                  "</cac:AccountingSupplierParty>";

            String detalle = "";

            foreach (EReversionDetalle item in reversion.DetalleReversion)
            {
                detalle = detalle + "<sac:VoidedDocumentsLine>" +
                            "<cbc:LineID>" + item.NumeroItem + "</cbc:LineID>" +
                            "<cbc:DocumentTypeCode>" + item.TipoDocumento + "</cbc:DocumentTypeCode>" +
                            "<sac:DocumentSerialID>" + item.Serie + "</sac:DocumentSerialID>" +
                            "<sac:DocumentNumberID>" + item.Correlativo + "</sac:DocumentNumberID>" +
                            "<sac:VoidReasonDescription>" + new Util().LimpiarCaracteresInvalidos(item.MotivoReversion) + "</sac:VoidReasonDescription>" +
                          "</sac:VoidedDocumentsLine>";
            }

            __xml = __xml + detalle;

            __xml = __xml + "</VoidedDocuments>";

            _xml.AppendLine(__xml.ToString());

            return _xml;
        }
        #endregion

        public EResultado ObtenerEstadoProcesoOSE(String numeroticket)
        {
            EResultado eresult = new EResultado();

            try
            {
                ServicePointManager.Expect100Continue = false;

                //OSE
                ClientCredentials clientCredentials = new ClientCredentials();
                clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                statusResponse statusresponse = proxy.getStatus(numeroticket);
                proxy.Close();

                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                String filenameZip = numeroticket + ".zip"; //notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";
                String filenameRptaXml = "";

                filenameZip = path + @"\CDR\" + filenameZip;

                byte[] oSalida = statusresponse.content;

                if (File.Exists(filenameZip))
                    File.Delete(filenameZip);

                new Util().SaveData(oSalida, filenameZip);

                using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameZip))
                {
                    foreach (ZipEntry fileZip in zip.Entries)
                    {
                        filenameRptaXml = path + @"\" + fileZip.FileName;
                        if (File.Exists(filenameRptaXml))
                            File.Delete(filenameRptaXml);
                    }
                    zip.ExtractAll(path);
                }

                //Leer Xml Respuesta Sunat
                XmlDocument xmlDocRptaSunat = new XmlDocument();
                xmlDocRptaSunat.Load(filenameRptaXml);

                XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                string responsecode = String.Empty, description = String.Empty;

                foreach (XmlNode nodo in lista)
                {
                    responsecode = nodo["cbc:ResponseCode"].InnerText;
                    description = nodo["cbc:Description"].InnerText;
                }

                /*
                string xmlRptaSunat = Encoding.UTF8.GetString(oSalida);
                XmlDocument xmlDocRptaSunat = new XmlDocument();
                xmlDocRptaSunat.LoadXml(xmlRptaSunat);

                XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ApplicationResponse");
                XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                string responsecode = String.Empty, description = String.Empty;

                foreach (XmlNode nodo in lista)
                {
                    responsecode = nodo["cbc:ResponseCode"].InnerText;
                    description = nodo["cbc:Description"].InnerText;
                }
                */

                String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                eresult.CDR = responsecode;
                eresult.EstadoComprobante = EstadoComprobante;
                eresult.MensajeResultadoSunat = description;
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = "";
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = String.Empty;
                eresult.MensajeResultadoSunat = mensajesunat;
            }

            return eresult;
        }

        public EResultado ObtenerEstadoCDROSE(string strRUC, string strTipo, string strSerie, int intCorrelativo, string strFecha)
        {
            EResultado eresult = new EResultado();

            try
            {
                string path = ConfigurationManager.AppSettings["RutaTemporal"].ToString();

                //Nombre de archivo
                String filenameZip = "R-" + strRUC + "-" + strTipo + "-" + strSerie + "-" + intCorrelativo.ToString() + ".zip"; //notadebito.RUCEmisor + "-" + notadebito.CodigoTipoNotaDebito + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[0] + "-" + notadebito.SerieNumeroCorrelativo.Split('-')[1] + ".zip";
                String filenameRptaXml = "";

                path = path + @"\" + strFecha.Substring(0, 4) + @"\" + strFecha.Substring(4, 2) + @"\" + strFecha.Substring(6, 2);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                filenameZip = path + @"\" + filenameZip;

                ServicePointManager.Expect100Continue = false;
                //TLS1.2
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                //ProxySunatCDR.billServiceClient proxy = new ProxySunatCDR.billServiceClient("BillConsultServicePort");
                //proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                //ProxySunatCDR.statusResponse statusresponse = proxy.getStatusCdr(strRUC, strTipo, strSerie, intCorrelativo);
                //proxy.Close();

                //OSE
                ClientCredentials clientCredentials = new ClientCredentials();
                clientCredentials.UserName.UserName = ConfigurationManager.AppSettings["OSE_USUARIO"];
                clientCredentials.UserName.Password = ConfigurationManager.AppSettings["OSE_PASSWORD"];

                IbillServiceClient proxy = new IbillServiceClient("CurrencyServiceSoapOSE");

                var defaultCredentials = proxy.ChannelFactory.Endpoint.Behaviors.Find<ClientCredentials>();
                proxy.ChannelFactory.Endpoint.Behaviors.Remove(defaultCredentials);

                proxy.ChannelFactory.Endpoint.Behaviors.Add(clientCredentials);

                proxy.InnerChannel.OperationTimeout = new TimeSpan(5, 30, 1);
                statusCdrResponse statusresponse = proxy.getStatusCdr(strRUC, strTipo, strSerie, intCorrelativo);
                proxy.Close();

                byte[] oSalida = statusresponse.content;

                if (oSalida != null)
                {
                    if (File.Exists(filenameZip))
                        File.Delete(filenameZip);

                    new Util().SaveData(oSalida, filenameZip);

                    using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(filenameZip))
                    {
                        foreach (ZipEntry fileZip in zip.Entries)
                        {
                            filenameRptaXml = path + @"\" + fileZip.FileName;
                            if (File.Exists(filenameRptaXml))
                                File.Delete(filenameRptaXml);
                        }
                        zip.ExtractAll(path);
                    }

                    //Leer Xml Respuesta Sunat
                    XmlDocument xmlDocRptaSunat = new XmlDocument();
                    xmlDocRptaSunat.Load(filenameRptaXml);

                    XmlNodeList ar = xmlDocRptaSunat.GetElementsByTagName("ar:ApplicationResponse");
                    XmlNodeList dr = ((XmlElement)ar[0]).GetElementsByTagName("cac:DocumentResponse");
                    XmlNodeList lista = ((XmlElement)dr[0]).GetElementsByTagName("cac:Response");

                    string responsecode = String.Empty, description = String.Empty;

                    foreach (XmlNode nodo in lista)
                    {
                        responsecode = nodo["cbc:ResponseCode"].InnerText;
                        description = nodo["cbc:Description"].InnerText;
                    }

                    String EstadoComprobante = responsecode == "0" ? "ACEPTADO" : "RECHAZADO";

                    eresult.CDR = responsecode;
                    eresult.EstadoComprobante = EstadoComprobante;
                    eresult.MensajeResultadoSunat = description;
                }
                else
                {
                    eresult.CDR = "9999";
                    eresult.EstadoComprobante = String.Empty;
                    eresult.MensajeResultadoSunat = "Archivo CDR no existe";
                }
            }
            catch (System.ServiceModel.FaultException ex)
            {
                String mensajesunat = ex.Message;
                System.Data.DataTable dtmensaje = FabricaSingleton<DComprobante>.Crear().ObtenerMensajeSunat(ex.Code.Name);

                if (dtmensaje.Rows.Count > 0)
                {
                    mensajesunat = dtmensaje.Rows[0]["Descripcion"] != null ? dtmensaje.Rows[0]["Descripcion"].ToString() : String.Empty;
                }

                eresult.CDR = ex.Code.Name;
                eresult.EstadoComprobante = String.Empty;
                eresult.MensajeResultadoSunat = mensajesunat;
            }

            return eresult;
        }
    }
}
