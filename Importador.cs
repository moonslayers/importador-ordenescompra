﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.Net.Http;
using Newtonsoft.Json;
using System.IO;

namespace ImportadorRemisiones
{
    public partial class Importador : Form
    {
        // bool Contpaqi = false;

        public Importador()
        {
            InitializeComponent();
        }

        private async void btnImportarContpaqi_ClickAsync(object sender, EventArgs e)
        {
            frmEspera frmEspera = new frmEspera();
            // Se conecta a una base de datos tipo mssql server para obtener datos de los articulos y tipo de cambio??
            Utileria utileria = new Utileria();

            tDocumento documento = new tDocumento();
            tMovimiento movimiento = new tMovimiento();
            int aIdDocumento = 0;
            int aIdMovimiento = 0;
            double lFolioDocto = 0;

            StringBuilder lSerieDocto = new StringBuilder(12);
            StringBuilder aMensaje = new StringBuilder(512);

            try
            {
                // AbrirEmpresa();
                // Contpaqi = true;

                int selectedRow = dgvRemisiones.Rows.GetRowCount(DataGridViewElementStates.Selected);

                Console.WriteLine("entrando al try");
                string folio = "";
                if (selectedRow == 1)
                {
                    // iva del cliente
                    //double ivaCliente = idcl > 0 ? utilsDb.GetIvaCliente(idcliente) : 8;

                    if (dgvRemisiones.SelectedRows[0].Cells[0].Value != null)
                    {
                        folio = dgvRemisiones.SelectedRows[0].Cells[0].Value.ToString();
                        //MessageBox.Show($"Id de orden de compra: {folio}");
                    }
                    else
                    {
                        MessageBox.Show($"Id de orden es nullo!");
                    }

                    // ojo este es el número de cliente NOO el id del cliente
                    string idcliente = dgvRemisiones.SelectedRows[0].Cells[2].Value.ToString();
                    int idcl = Int32.Parse(idcliente); // No. Cliente y NO el id del cliente

                    //MessageBox.Show($"Id del cliente proveedor: {idcl}");

 

                    
                    //Esto no es necesario porque el primer get del node api ya da ciertos datos del cliente

                    string clienteStr = await utileria.getClienteRow(idcliente);
                    JsonTextReader ClienteJson = new JsonTextReader(new StringReader(clienteStr));

                    string clienteId = valueJson(ClienteJson, "CCODIGOCLIENTE");
                    //MessageBox.Show("clienteproveedor: "+ clienteId.ToString());


                    //MessageBox.Show(nocliente + ", " + folio);
                    DialogResult result = MessageBox.Show(
                            "Seguro de Importar la Orden de Compra??", "Importar",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                    if (result == DialogResult.Yes)
                    {
                        //Esta es una pantalla de carga para indicar que esta conectando a contpaq
                        frmEspera.Show();
                        // -- obtencion del folio de contpaq
                        //Esto lo comente porque se necesita tener contpaq activado
                        int lError = ComercialSdk.fSiguienteFolio(cmbTipoFactura.SelectedValue.ToString(), lSerieDocto, ref lFolioDocto);

                        if (lError != 0)
                        {
                            // MessageBox.Show("(fSiguienteFolio) Error no: " + lError);
                            ComercialSdk.fError(lError, aMensaje, 512);
                            MessageBox.Show("fSiguienteFolio - Error: " + aMensaje);
                            frmEspera.Close();

                            return;
                        }

                        // -- definición del documento
                        documento.aSerie = lSerieDocto.ToString();
                        documento.aFolio = lFolioDocto;
                        documento.aNumMoneda = Utileria.GetTipoMoneda(cmbTipoFactura.SelectedValue.ToString());
                        documento.aTipoCambio = Convert.ToDouble(txtTipoCambio.Text);
                        documento.aCodConcepto = cmbTipoFactura.SelectedValue.ToString();
                        documento.aSistemaOrigen = 6;
                        documento.aFecha = DateTime.Now.ToString("MM/dd/yyyy");
                        // documento.aCodigoCteProv = idcl > 0 ?  nocliente.PadLeft(4, '0') : "999";
                        //aqui asignamos el codigo de cliente proveedor
                        documento.aCodigoCteProv = clienteId.Length > 0 ? clienteId : "999";

                        // -- creando el documento
                        lError = ComercialSdk.fAltaDocumento(ref aIdDocumento, ref documento);
                        if (lError != 0)
                        {
                            // MessageBox.Show("fAltaDocumento - Error no: " + lError);
                            ComercialSdk.fError(lError, aMensaje, 512);
                            MessageBox.Show("fAltaDocumento - Error: " + aMensaje);
                            frmEspera.Close();

                            return;
                        }

                        // -- añadiendo los campos extras TODO: Que pasa cuando no hay número de cliente
                        //DataRow cliente = Utileria.GetCliente(idcliente);
                        //Int32.TryParse(cliente["dias_credito"].ToString(), out int diasCredito);

                        //int dCredito = diasCredito > 0 ? diasCredito : 0;
                        //por el momento 0 dias de credito porque es una compra, el credito lo asigna quien vende
                        int dCredito = 0;

                        DateTime fechaVencimiento = DateTime.Now.Add(TimeSpan.FromDays(dCredito));

                        lError = ComercialSdk.fBuscarIdDocumento(aIdDocumento);
                        if (lError != 0)
                        {
                            ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                            MessageBox.Show("fBuscarIdDocumento - Error: " + aMensaje.ToString());
                            frmEspera.Close();

                            return;
                        }
                        
                        //asignamos un folio interno que correspone a OC-<numero de folio orden>
                        ComercialSdk.fSetDatoDocumento("Ctextoex01", $"OC-{folio}");
                        ComercialSdk.fSetDatoDocumento("Clugarexpe", "AYUNTAMIENTO 698,EX EJIDO COAHUILA, 21360, MEXICALI, BAJA CALIFORNIA, MEXICO");
                        ComercialSdk.fSetDatoDocumento("CFECHAVENCIMIENTO", fechaVencimiento.ToString("MM/dd/yyyy"));
                        
                        lError = ComercialSdk.fGuardaDocumento();
                        if (lError != 0)
                        {
                            ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                            MessageBox.Show("fGuradaDocumento - Error: " + aMensaje.ToString());
                            frmEspera.Close();

                            return;
                        }


                        //  Alta de los Movimientos

                        //DataTable productos = db.ResultQuery("SELECT *,GET_NOPLANO(idart) as noplano FROM tblremisiones_art where idrem=" + idRemision);
                        DataTable productos = await utileria.getProductos(folio,idcl.ToString());

                        if (productos != null)
                        {
                            if (productos.Rows.Count > 0)
                            {
                                foreach (DataRow row in productos.Rows)
                                {
                                    //necesito actualizar el servidor para mandar aprecio
                                    double precioArticulo = double.Parse(row["aprecio"].ToString());
                                    double precioTruncado = 0;
                                    // buscar el articulo
                                    string codArticulo = row["CCODIGOPRODUCTO"].ToString();

                                    if (precioArticulo > 0)
                                    {
                                        precioTruncado = Math.Truncate(precioArticulo * 100) / 100;
                                    }
                                    else
                                    {
                                        MessageBox.Show("Error: El precio del articulo es 0.0: " + codArticulo);

                                        return;
                                    }

                                    lError = 0;
                                    lError = ComercialSdk.fBuscaProducto(codArticulo);
                                    if (lError != 0)
                                    {
                                        ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                                        MessageBox.Show(aMensaje.ToString());
                                        frmEspera.Close();
                                        MessageBox.Show("No se encontro el articulo: " + codArticulo);

                                        return;
                                    }

                                    // -- unidades
                                    double.TryParse(row["acantidad"].ToString(), out double unidades);

                                    // Generando Movimiento
                                    movimiento.aConsecutivo = 1;
                                    movimiento.aUnidades = unidades;
                                    movimiento.aPrecio = precioArticulo; // precioTruncado;
                                    movimiento.aCodProdSer = row["CCODIGOPRODUCTO"].ToString(); // utilsDb.GetNoPlanoArticulo(row["idart"].ToString());
                                    movimiento.aCodAlmacen = row["CCODIGOALMACEN"].ToString();
                                    //TODO: hasta aqui esta correcto.
                                    // movimiento.aReferencia = "0";
                                    // movimiento.aCodClasificacion = "0";

                                    // Acción de la alta
                                    lError = 0;
                                    lError = ComercialSdk.fAltaMovimiento(aIdDocumento, ref aIdMovimiento, ref movimiento);

                                    if (lError != 0)
                                    {
                                        ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                                        MessageBox.Show("fAltaMoviemiento - Error: " + aMensaje.ToString());
                                        frmEspera.Close();

                                        return;
                                    }

                                    // Campos extras del Movimiento
                                    lError = 0;
                                    lError = ComercialSdk.fBuscarIdMovimiento(aIdMovimiento);

                                    if (lError != 0)
                                    {
                                        ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                                        MessageBox.Show("fBuscarIdMovimiento - Erro: " + aMensaje.ToString());
                                        frmEspera.Close();

                                        return;
                                    }

                                    //ESTO NO ES NECESARIO PARA ORDENES
                                    /*double pesoLb = utilsDb.GetPesoArticulo(row["idart"].ToString());
                                    double pesoKg = pesoLb * unidades * 0.45;
                                    // -- Conversión a entero el peso
                                    int pesoKgInt = Convert.ToInt32(Math.Ceiling(pesoLb));

                                    // pies2
                                    double pies = utilsDb.Pies2Articulo(row["idart"].ToString()) * unidades;
                                    // Convertir los pies a entero
                                    int piesInt = Convert.ToInt32(Math.Ceiling(pies));

                                    // Orden Compra
                                    string po = utilsDb.GetPoRemision(row["idra"].ToString());

                                    // resistencia
                                    string resistencia = utilsDb.GetResistenciaLaminaAsignadaArticulo(row["idart"].ToString());

                                    // -- iva del articulo en base del cliente
                                    double netoArticulo = unidades * precioArticulo;
                                    double resultIva = netoArticulo * (ivaCliente / 100);

                                    double ivaArticulo = Math.Round(resultIva, 2);

                                    lError = 0;
                                    lError = ComercialSdk.fEditarMovimiento();
                                    lError = ComercialSdk.fSetDatoMovimiento("Ctextoex01", pesoKgInt.ToString()); // -- entero
                                    lError = ComercialSdk.fSetDatoMovimiento("Ctextoex02", piesInt.ToString()); // -- entro
                                    lError = ComercialSdk.fSetDatoMovimiento("Ctextoex03", po);
                                    lError = ComercialSdk.fSetDatoMovimiento("Creferen01", resistencia);
                                    lError = ComercialSdk.fSetDatoMovimiento("Cimpuesto1", ivaArticulo.ToString());*/

                                    //Esto comente
                                    lError = ComercialSdk.fGuardaMovimiento();

                                    if (lError != 0)
                                    {
                                        ComercialSdk.fError(lError, aMensaje, Constantes.kLongMensaje);
                                        MessageBox.Show("fGuardaMovimiento - Error: " + aMensaje.ToString());
                                        frmEspera.Close();

                                        return;
                                    }
                                  
                                }
                            }
                            else
                            {
                                MessageBox.Show("La lista de productos esta vacia");
                                return;
                            }
                        }
                        else {
                            MessageBox.Show("La lista de productos es nula o vacia");
                            return;
                        }
                        List<object> listaObjetos = ConvertirDataTableALista(productos);

                        // Convierte la lista de objetos a formato JSON
                        string json = JsonConvert.SerializeObject(listaObjetos);

                        // Bandera de Factura Importada
                        //recordatorio, hacer funcion para marcar la orden como importada es decir oestatus=2
                        await utileria.marcarProductosImportados(json);
                        
                        // leer nuevamente las remisiones
                        this.btnVerRemisiones_ClickAsync(sender, e);

                        
                        
                        frmEspera.Close();

                        // CerrarEmpresa();

                        MessageBox.Show("ORDEN DE COMPRA IMPORTADA!");
                    } // -- if del código

                }
                else
                {
                    MessageBox.Show("Seleccione solo una remisión de la tabla!");
                    return;
                }

                /* if (Contpaqi)
                {
                }
                else
                {
                    MessageBox.Show("No hay conección con Contpaqi");
                } */
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excepcion: {ex.Message}");
            }
        }
        static List<object> ConvertirDataTableALista(DataTable dataTable)
        {
            List<object> listaObjetos = new List<object>();
            if (dataTable != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    // Convierte cada DataRow a un objeto anónimo y agrégalo a la lista
                    var objetoAnonimo = new
                    {
                        aid = row["aid"],
                    };

                    listaObjetos.Add(objetoAnonimo);
                }
            }
            return listaObjetos;
        }

        private string valueJson(JsonTextReader reader, string key)
        {
            while (reader.Read())
            {
                //Console.WriteLine("TokenType: {0}, Value: {1}", reader.TokenType, reader.Value);
                if(reader.Value != null) {
                    if (reader.Value.ToString() == key)
                    {
                        reader.Read();
                        if (reader.Value != null)
                        {
                            //Console.WriteLine("Key: {0}, Value: {1}", key, reader.Value);
                            return reader.Value.ToString();
                        }
                        else
                        {
                            Console.WriteLine("Token: {0}, Value: null", reader.TokenType);
                            return "";
                        }
                    }
                }
            }
            return "";
        }
        

        private void Importador_Load(object sender, EventArgs e)
        {
            //no usamos contpaq por el momento de pruebas
            AbrirEmpresa();

            BindingList<TipoFactura> tpFactura = new BindingList<TipoFactura>
            {
                // BindingList<Moneda> tpMoneda = new BindingList<Moneda>();
                // tipo facturas
                new TipoFactura("21","Compra"),
                new TipoFactura("211","Compras Agroquimicos Pesos"),
                new TipoFactura("212","Compras Agroquimicos Dolares"),
                new TipoFactura("213","Compras Repuestos y Accesorios PESOS"),
                new TipoFactura("214","Compras Respuestos y Accesorios DOLARES"),
                new TipoFactura("215","Compras Material Empaque PESOS"),
                new TipoFactura("216","Compras Material Empaque DOLARES"),
                new TipoFactura("217","Compras SEMILLA"),
                new TipoFactura("218","Compra de Material de Empaque CHINA"),
            };

            cmbTipoFactura.DataSource = tpFactura;
            cmbTipoFactura.DisplayMember = "Tipo";
            cmbTipoFactura.ValueMember = "ClaveDocumento";

            //Contpaqi = true; // -- temporal

            // Obtener el tipo de cambio
            //MySqlDatabase dbase = new MySqlDatabase();
            //float tc = dbase.GetTc();
            txtTipoCambio.Text = "17.00";
        }

        private void Importador_FormClosing(object sender, FormClosingEventArgs e)
        {
            CerrarEmpresa();
        }

        private async void btnVerRemisiones_ClickAsync(object sender, EventArgs e)
        {
            frmEspera esperaFrm = new frmEspera();
            esperaFrm.Show();

            try
            {
                // Endpoint del API
                //string apiUrl = "http://3.132.141.153:3000/data/ordenes-compra"; // Reemplaza con la URL correcta de tu API
                //string apiUrl = "http://localhost:3000/data/ordenes-compra";
                string apiUrl = string.Format(ConfigurationManager.AppSettings["APINODE"]) + "data/ordenes-compra";
                //MessageBox.Show(apiUrl);


                using (HttpClient client = new HttpClient())
                {
                    // Realizar una solicitud GET al API
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        // Leer el contenido de la respuesta
                        string apiResponse = await response.Content.ReadAsStringAsync();

                        // Convertir el JSON (si es un JSON) a un DataTable o cualquier estructura de datos que desees
                        DataTable dataTable = ConvertJsonToDataTable(apiResponse);

                        // Asignar el DataTable al DataGridView
                        dgvRemisiones.DataSource = dataTable;
                    }
                    else
                    {
                        // Manejar el caso en que la solicitud al API no sea exitosa
                        Console.WriteLine("Error en la solicitud al API. Código de estado: " + response.StatusCode);
                        MessageBox.Show("Error al hacer la solicitud");
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
            finally
            {
                esperaFrm.Close();
            }
        }

        // Método para convertir JSON a DataTable (puedes ajustar según tu estructura de datos)
        private DataTable ConvertJsonToDataTable(string json)
        {
            DataTable dataTable = JsonConvert.DeserializeObject<DataTable>(json);

            return dataTable;
        }

        private void AbrirEmpresa()
        {
            // Contpaqi = true;

            var appSettings = ConfigurationManager.AppSettings;
            string lRutaBinarios = appSettings["RutaBinarios"];
            string lEmpresa = appSettings["Empresa"];
            StringBuilder sMensaje = new StringBuilder(512);

            // -- Establecer el directorio de Comercial
            ComercialSdk.SetCurrentDirectory(lRutaBinarios);

            // -- Inicializar el SDK de Contpaq
            int lError = ComercialSdk.fSetNombrePAQ("CONTPAQ I Comercial");
            if (lError != 0)
            {
                ComercialSdk.fError(lError, sMensaje, 512);
                MessageBox.Show("Error: " + sMensaje);
            }
            else
            {
                // -- Abrir Empresa
                ComercialSdk.fAbreEmpresa(lEmpresa);
                // MessageBox.Show("Empresa Abirta!");
                // Contpaqi = true;
            }
        }

        private void CerrarEmpresa()
        {
            // Contpaqi = false;
            ComercialSdk.fCierraEmpresa();
            ComercialSdk.fTerminaSDK();
        }

        private void tsBtnTipoCambio_Click(object sender, EventArgs e)
        {
            frmTipoCambio tipoCambio = new frmTipoCambio();
            if (tipoCambio.ShowDialog() == DialogResult.OK)
            {
                // Verificar si el nuevo tipo de cambio es un número válido
                if (float.TryParse(tipoCambio.NuevoTipoCambio, out float nuevoTipoCambioFloat))
                {
                    // Si es un número válido, lo asignamos al TextBox
                    this.txtTipoCambio.Text = nuevoTipoCambioFloat.ToString("0.00");
                }
                else
                {
                    // Mostrar un mensaje de error si no es un número válido
                    MessageBox.Show("Ingrese un valor numérico válido para el tipo de cambio.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void txtTipoCambio_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
