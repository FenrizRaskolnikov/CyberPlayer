using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace CyberPlayer
{
    
    
    public partial class MainWindow : Window
    {
        // El output de audio (los parlantes/audífonos de la PC)
        private WaveOutEvent outputDevice;

        // El lector del archivo de audio (el que decodifica el archivo MP3)
        // El lector del archivo de audio (el que decodifica el archivo MP3)
        private AudioFileReader audioFile;

        // NUEVO: Lista oculta en memoria para guardar las rutas reales de los archivos
        private List<string> rutasArchivosReales = new List<string>();
        // Variable para almacenar de forma persistente la cola de reproducción actual
        private List<string> colaDeReproduccion = new List<string>();
        // El índice real de la canción que está sonando dentro de la cola en memoria
        private int indiceEnColaActual = -1;
        // Guardan la altura actual de los picos flotantes (0 a 60 que es el alto del visualizador)
        private double[] picosAlturas = new double[8];
        // Controlan la velocidad de caída de cada pico para simular gravedad
        private double[] picosVelocidadCaida = new double[8];

        // Definición de colores Cyberpunk para el degradado dinámico por software
        private readonly System.Windows.Media.Brush colorBajo = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#00FF66"); // Verde Neón
        private readonly System.Windows.Media.Brush colorMedio = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#7F00FF"); // Violeta
        private readonly System.Windows.Media.Brush colorAlto = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF007F"); // Rosa Cyberpunk

        public MainWindow()
        {
            InitializeComponent();
            BtnVolver.Visibility = Visibility.Collapsed;
            relojTiempo = new System.Windows.Threading.DispatcherTimer();
            relojTiempo.Interval = TimeSpan.FromMilliseconds(500);
            relojTiempo.Tick += RelojTiempo_Tick;
            
        }


        private void BtnCargarCarpeta_Click(object sender, RoutedEventArgs e)
        {
            // 1. Creamos el cuadro de diálogo nativo de Windows
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "Selecciona tu biblioteca de música Cybergoth";

            // Si el usuario selecciona la carpeta y acepta
            if (dialog.ShowDialog() == true)
            {
                string carpetaSeleccionada = dialog.FolderName;

                try
                {
                    biblioteca.CargarCarpeta(carpetaSeleccionada);

                    // Activamos la vista general por defecto
                    vistaActual = 0;
                    ActualizarColoresPestañas(BtnVistaTodas);
                    MostrarTodasLasCanciones();

                    if (biblioteca.TodasLasCanciones.Count == 0)
                    {
                        ListaCanciones.Items.Add("[ No se encontraron archivos .mp3 en esta carpeta ]");
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al procesar la biblioteca: " + ex.Message);
                }
            }
        }
        // Muestra la lista global completa (tal como funciona ahora)
        private void BtnVistaTodas_Click(object sender, RoutedEventArgs e)
        {
            artistaFiltradoActual = null;
            vistaActual = 0; // 0 = Todas
            ActualizarColoresPestañas(BtnVistaTodas);
            MostrarTodasLasCanciones();
            ActualizarVisibilidadVolver();
        }

        // Cambia la lista verde para mostrar los nombres de los artistas
        private void BtnVistaArtistas_Click(object sender, RoutedEventArgs e)
        {
            
            vistaActual = 1; // 1 = Artistas
            ActualizarColoresPestañas(BtnVistaArtistas);

            ListaCanciones.Items.Clear();
            rutasArchivosReales.Clear(); // En esta vista no hay rutas físicas directas

            foreach (var artista in biblioteca.Artistas)
            {
                ListaCanciones.Items.Add($"[ARTISTA] > {artista.Nombre}");
            }
            ActualizarVisibilidadVolver();
        }

        // Cambia la lista verde para mostrar todos los álbumes
        private void BtnVistaAlbumes_Click(object sender, RoutedEventArgs e)
        {
            artistaFiltradoActual = null;
            vistaActual = 2; // 2 = Álbumes
            ActualizarColoresPestañas(BtnVistaAlbumes);

            ListaCanciones.Items.Clear();
            rutasArchivosReales.Clear();

            foreach (var album in biblioteca.TodosLosAlbumes)
            {
                ListaCanciones.Items.Add($"[ÁLBUM] > {album.Nombre} ({album.Artista})");
            }
            ActualizarVisibilidadVolver();
        }
        // Función para rellenar la lista con todo el catálogo disponible
        private void MostrarTodasLasCanciones()
        {
            ListaCanciones.Items.Clear();
            rutasArchivosReales.Clear();

            foreach (var cancion in biblioteca.TodasLasCanciones)
            {
                rutasArchivosReales.Add(cancion.RutaCompleta);
                ListaCanciones.Items.Add($"{cancion.Artista} - {cancion.Album} > {cancion.Titulo}");
            }
        }

        // Hace que la pestaña seleccionada brille en verde neón y las demás queden atenuadas en blanco
        private void ActualizarColoresPestañas(Button pestañaActiva)
        {
            BtnVistaTodas.Foreground = Brushes.White;
            BtnVistaArtistas.Foreground = Brushes.White;
            BtnVistaAlbumes.Foreground = Brushes.White;

            var convertidorColor = new System.Windows.Media.ColorConverter();
            var colorVerdeNeon = new SolidColorBrush((Color)convertidorColor.ConvertFrom("#00FF66"));

            pestañaActiva.Foreground = colorVerdeNeon;
        }
        // NUEVO: Variable para saber cuál fue la última canción que se cargó físicamente en el motor
        private int indiceCancionActual = -1;

        // Estados de reproducción
        private bool modoAleatorio = false;

        // 0 = Desactivado, 1 = Repetir una canción, 2 = Repetir toda la lista
        private int estadoRepeticion = 0;
        private int vistaActual = 0;
        private bool fueDetenidoManualmente = false;

        // Para el modo aleatorio: guarda el historial de lo que ya sonó para no repetir
        private Random random = new Random();

        // Reloj interno para actualizar la barra de tiempo segundo a segundo
        private System.Windows.Threading.DispatcherTimer relojTiempo;
        private bool usuarioArrastrandoSlider = false;
        private SampleChannel canalMuestras;
        // Instancia única de nuestra lógica de base de datos musical
        private BibliotecaMusical biblioteca = new BibliotecaMusical();
        // Mantiene el rastro de qué pestaña está mirando el usuario: 0 = Todas, 1 = Artistas, 2 = Álbumes
        // Guardan la referencia temporal de lo que el usuario está explorando
        private ArtistaInfo artistaFiltradoActual;
        private AlbumInfo albumFiltradoActual;


        // 1. FUNCIÓN MAESTRA: Se encarga de apagar el motor viejo y cargar el archivo nuevo
        private void ReproducirCancionSeleccionada()
        {
            if (ListaCanciones.SelectedIndex >= 0)
            {
                int indiceSeleccionado = ListaCanciones.SelectedIndex;
                if (indiceSeleccionado >= rutasArchivosReales.Count) return;

                // --- SISTEMA DE ACOPLAMIENTO DE COLA INTELIGENTE ---
                // Si el usuario hace doble clic en una pista, "congelamos" la vista actual como nuestra cola de reproducción
                if (colaDeReproduccion.Count == 0 || !ListasSonIdenticas(rutasArchivosReales, colaDeReproduccion) || indiceCancionActual != indiceSeleccionado)
                {
                    colaDeReproduccion = new List<string>(rutasArchivosReales);
                    indiceEnColaActual = indiceSeleccionado;
                }

                string rutaArchivo = colaDeReproduccion[indiceEnColaActual];

                try
                {
                    // ===================================================================
                    // 🛠️ EXTRAER METADATOS Y CARÁTULA
                    // ===================================================================
                    try
                    {
                        using (var tagFile = TagLib.File.Create(rutaArchivo))
                        {
                            string titulo = !string.IsNullOrEmpty(tagFile.Tag.Title)
                                ? tagFile.Tag.Title
                                : System.IO.Path.GetFileNameWithoutExtension(rutaArchivo);

                            string artista = (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0)
                                ? tagFile.Tag.Performers[0]
                                : "Artista Desconocido";

                            TxtTituloActual.Text = titulo.ToUpper();
                            TxtArtistaActual.Text = artista.ToUpper();

                            if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                            {
                                var pic = tagFile.Tag.Pictures[0];
                                byte[] bytesImagen = pic.Data.Data;

                                using (var ms = new System.IO.MemoryStream(bytesImagen))
                                {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    ImgCaratula.Source = bitmap;
                                }
                            }
                            else
                            {
                                ImgCaratula.Source = null;
                            }
                        }
                    }
                    catch
                    {
                        TxtTituloActual.Text = System.IO.Path.GetFileNameWithoutExtension(rutaArchivo).ToUpper();
                        TxtArtistaActual.Text = "ARTISTA DESCONOCIDO";
                        ImgCaratula.Source = null;
                    }
                    // ===================================================================

                    // --- Lógica de NAudio para limpiar hilos ---
                    if (outputDevice != null)
                    {
                        outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                        outputDevice.Stop();
                        outputDevice.Dispose();
                        outputDevice = null;
                    }
                    if (audioFile != null)
                    {
                        audioFile.Dispose();
                        audioFile = null;
                    }

                    audioFile = new AudioFileReader(rutaArchivo);

                    canalMuestras = new SampleChannel(audioFile);
                    canalMuestras.PreVolumeMeter += CanalMuestras_PreVolumeMeter;

                    outputDevice = new WaveOutEvent();
                    outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;

                    audioFile.Volume = (float)SliderVolumen.Value;

                    outputDevice.Init(canalMuestras);
                    outputDevice.Play();

                    // Sincronizamos las variables del motor
                    indiceCancionActual = indiceSeleccionado;

                    // Si visualmente la pantalla muestra la misma lista de la que estamos reproduciendo, sincronizamos el foco visual
                    if (ListasSonIdenticas(rutasArchivosReales, colaDeReproduccion))
                    {
                        ListaCanciones.SelectedIndex = indiceEnColaActual;
                    }

                    SliderProgreso.Maximum = audioFile.TotalTime.TotalSeconds;
                    SliderProgreso.Value = 0;
                    TxtTiempoTotal.Text = audioFile.TotalTime.ToString(@"mm\:ss");
                    TxtTiempoActual.Text = "00:00";

                    relojTiempo.Start();
                    ActualizarBotonPlayPause(true);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al reproducir el archivo: " + ex.Message);
                }
            }
        }
        private string archivoTemporalActivo = null;
        private void ReproducirCancionDesdeCola()
        {
            if (indiceEnColaActual < 0 || indiceEnColaActual >= colaDeReproduccion.Count) return;
            string rutaArchivoOriginal = colaDeReproduccion[indiceEnColaActual];

            try
            {
                // 1. Extraer metadatos
                try
                {
                    using (var tagFile = TagLib.File.Create(rutaArchivoOriginal))
                    {
                        string titulo = !string.IsNullOrEmpty(tagFile.Tag.Title) ? tagFile.Tag.Title : System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal);
                        string artista = (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0) ? tagFile.Tag.Performers[0] : "Artista Desconocido";

                        TxtTituloActual.Text = titulo.ToUpper();
                        TxtArtistaActual.Text = artista.ToUpper();

                        if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                        {
                            var pic = tagFile.Tag.Pictures[0];
                            byte[] bytesImagen = pic.Data.Data;

                            // CORRECCIÓN AQUÍ: Forzar la carga en RAM para no bloquear el MP3 original
                            using (var ms = new System.IO.MemoryStream(bytesImagen))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad; // <-- CRÍTICO
                                bitmap.StreamSource = ms;
                                bitmap.EndInit();
                                bitmap.Freeze(); // <-- CRÍTICO
                                ImgCaratula.Source = bitmap;
                            }
                        }
                        else { ImgCaratula.Source = null; }
                    }
                }
                catch
                {
                    TxtTituloActual.Text = System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal).ToUpper();
                    TxtArtistaActual.Text = "ARTISTA DESCONOCIDO";
                    ImgCaratula.Source = null;
                }

                // 2. Apagar motor viejo de forma limpia antes de liberar recursos
                if (outputDevice != null)
                {
                    outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                    outputDevice.Stop();
                    outputDevice.Dispose();
                    outputDevice = null;
                }
                if (audioFile != null)
                {
                    audioFile.Dispose();
                    audioFile = null;
                }

                // 3. Limpiar el archivo temporal de la canción anterior si existía
                EliminarArchivoTemporal();

                // 4. Crear la copia temporal para que NAudio no bloquee el archivo original
                try
                {
                    string extension = System.IO.Path.GetExtension(rutaArchivoOriginal);
                    archivoTemporalActivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CyberPlayer_Temp_{Guid.NewGuid()}{extension}");

                    System.IO.File.Copy(rutaArchivoOriginal, archivoTemporalActivo, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al preparar el archivo temporal: " + ex.Message);
                    return;
                }

                // 5. Encender el nuevo track usando la COPIA TEMPORAL
                audioFile = new AudioFileReader(archivoTemporalActivo);
                canalMuestras = new SampleChannel(audioFile);
                canalMuestras.PreVolumeMeter += CanalMuestras_PreVolumeMeter;

                outputDevice = new WaveOutEvent();
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                audioFile.Volume = (float)SliderVolumen.Value;

                outputDevice.Init(canalMuestras);
                outputDevice.Play();

                // Si la interfaz de pantalla está mostrando la lista correspondiente a la cola, actualizamos la selección visual
                if (ListasSonIdenticas(rutasArchivosReales, colaDeReproduccion))
                {
                    indiceCancionActual = indiceEnColaActual;
                    ListaCanciones.SelectedIndex = indiceEnColaActual;
                }
                else
                {
                    // Si el usuario está explorando otras vistas (ej. artistas), dejamos el índice visual en -1 para no confundir
                    indiceCancionActual = -1;
                }

                SliderProgreso.Maximum = audioFile.TotalTime.TotalSeconds;
                SliderProgreso.Value = 0;
                TxtTiempoTotal.Text = audioFile.TotalTime.ToString(@"mm\:ss");
                TxtTiempoActual.Text = "00:00";

                relojTiempo.Start();
                ActualizarBotonPlayPause(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en la cola de reproducción: " + ex.Message);
            }
        }

        // Asegúrate también de incluir este método auxiliar en tu código para borrar la basura del disco
        private void EliminarArchivoTemporal()
        {
            try
            {
                if (!string.IsNullOrEmpty(archivoTemporalActivo) && System.IO.File.Exists(archivoTemporalActivo))
                {
                    System.IO.File.Delete(archivoTemporalActivo);
                    archivoTemporalActivo = null;
                }
            }
            catch
            {
                // Se ignora si el archivo sigue retenido milisegundos extra por el sistema operativo, 
                // se limpiará en la próxima reproducción o al cerrar la app.
            }
        }

        // Función auxiliar para comparar si la lista visual coincide con la cola de audio
        private bool ListasSonIdenticas(List<string> listaA, List<string> listaB)
        {
            if (listaA.Count != listaB.Count) return false;
            for (int i = 0; i < listaA.Count; i++)
            {
                if (listaA[i] != listaB[i]) return false;
            }
            return true;
        }

        // 2. EL BOTÓN INTELIGENTE: Ahora analiza si cambiaste de opinión en la lista verde
        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (ListaCanciones.SelectedIndex < 0 && colaDeReproduccion.Count == 0) return;

            // Si el usuario seleccionó visualmente otra canción en la interfaz que es distinta a la que suena
            if (ListaCanciones.SelectedIndex >= 0 && ListaCanciones.SelectedIndex != indiceCancionActual && (vistaActual == 0 || vistaActual == 4))
            {
                ReproducirCancionSeleccionada();
                return;
            }

            // Comportamiento clásico Play/Pause sobre el dispositivo activo
            if (outputDevice != null)
            {
                if (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    outputDevice.Pause();
                    ActualizarBotonPlayPause(false);
                }
                else
                {
                    // ===================================================================
                    // 🛠️ PARCHE DEL BUG DE STOP: 
                    // Si la música estaba completamente detenida (por ejemplo, tras usar STOP) 
                    // y los textos de la interfaz están limpios, volvemos a cargar la info.
                    // ===================================================================
                    if (TxtTituloActual.Text == "SISTEMA DETENIDO" && indiceEnColaActual >= 0 && indiceEnColaActual < colaDeReproduccion.Count)
                    {
                        // En lugar de hacer un simple .Play(), forzamos la recarga completa 
                        // desde la cola para que se reconstruya la interfaz (carátula, textos, etc.)
                        ReproducirCancionDesdeCola();
                    }
                    else
                    {
                        // Si solo estaba en pausa normal, reanudamos de manera clásica y ligera
                        outputDevice.Play();
                        ActualizarBotonPlayPause(true);
                    }
                }
            }
            else
            {
                // Si el motor estaba apagado (por ejemplo, al iniciar) pero hay algo seleccionado, le damos arranque
                if (ListaCanciones.SelectedIndex >= 0)
                {
                    ReproducirCancionSeleccionada();
                }
            }
        }

        // 3. EL DOBLE CLIC: Ahora simplemente delega el trabajo a la función maestra
        private void ListaCanciones_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int seleccionado = ListaCanciones.SelectedIndex;
            if (seleccionado < 0) return;

            if (vistaActual == 0 || vistaActual == 4)
            {
                // ➔ CASO 0 o 4: Son canciones reales, se reproducen.
                ReproducirCancionSeleccionada();
            }
            else if (vistaActual == 1)
            {
                // ➔ CASO 1: Estamos en la lista global de ARTISTAS. 
                if (seleccionado < biblioteca.Artistas.Count)
                {
                    artistaFiltradoActual = biblioteca.Artistas[seleccionado];
                    vistaActual = 3; // Cambiamos a estado: Viendo álbumes de un artista

                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var album in artistaFiltradoActual.Albumes)
                    {
                        // Usamos album.Nombre porque así está en tu AlbumInfo
                        ListaCanciones.Items.Add($"[DISCO] > {album.Nombre}");
                    }
                }
            }
            else if (vistaActual == 2)
            {
                // ➔ CASO 2: Estamos en la lista global de ÁLBUMES.
                if (seleccionado < biblioteca.TodosLosAlbumes.Count)
                {
                    albumFiltradoActual = biblioteca.TodosLosAlbumes[seleccionado];
                    vistaActual = 4; // Cambiamos a estado: Viendo canciones de un álbum

                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var cancion in albumFiltradoActual.Canciones)
                    {
                        rutasArchivosReales.Add(cancion.RutaCompleta);
                        ListaCanciones.Items.Add($"{cancion.Titulo}");
                    }
                }
            }
            else if (vistaActual == 3)
            {
                // ➔ CASO 3: Estábamos viendo los discos de un artista específico y elegimos uno.
                if (seleccionado < artistaFiltradoActual.Albumes.Count)
                {
                    albumFiltradoActual = artistaFiltradoActual.Albumes[seleccionado];
                    vistaActual = 4; // Cambiamos a estado: Viendo canciones de un álbum

                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var cancion in albumFiltradoActual.Canciones)
                    {
                        rutasArchivosReales.Add(cancion.RutaCompleta);
                        ListaCanciones.Items.Add($"{cancion.Titulo}");
                    }
                }
            }
            ActualizarVisibilidadVolver();
        }
        // Método para detener la reproducción por completo
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔥 Indicamos que fuimos nosotros quienes paramos el motor
                fueDetenidoManualmente = true;

                if (outputDevice != null)
                {
                    outputDevice.Stop();
                }

                if (audioFile != null)
                {
                    audioFile.Position = 0;
                }

                // 🔥 Detenemos el reloj y reseteamos el ecualizador visual
                relojTiempo.Stop();
                ResetearBarrasVisualizador();

                SliderProgreso.Value = 0;
                TxtTiempoActual.Text = "00:00";
                ActualizarBotonPlayPause(false);

                // ===================================================================
                // 🛠️ NUEVO: LIMPIEZA DE METADATOS AL DETENER LA MÚSICA (Paso 3)
                // ===================================================================
                TxtTituloActual.Text = "SISTEMA DETENIDO";
                TxtArtistaActual.Text = "SELECCIONA UNA PISTA";
                ImgCaratula.Source = null; // Quita la imagen actual de la pantalla
                                           // ===================================================================
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al detener la reproducción: {ex.Message}");
            }
        }

        // FUNCIÓN AUXILIAR: Cambia el diseño del botón híbrido para no repetir código visual
        private void ActualizarBotonPlayPause(bool estaReproduciendo)
        {
            var convertidorColor = new System.Windows.Media.ColorConverter();

            if (estaReproduciendo)
            {
                // Si ESTÁ SONANDO, el botón debe invitar a PAUSAR (Fucsia)
                BtnPlayPause.Content = "⏸ PAUSE";
                var colorFucsia = (Color)convertidorColor.ConvertFrom("#FF007F");
                BtnPlayPause.Foreground = new SolidColorBrush(colorFucsia);
                BtnPlayPause.BorderBrush = new SolidColorBrush(colorFucsia);
            }
            else
            {
                // Si ESTÁ PAUSADO o DETENIDO, el botón debe invitar a dar PLAY (Verde)
                BtnPlayPause.Content = "▶ PLAY";
                var colorVerde = (Color)convertidorColor.ConvertFrom("#00FF66");
                BtnPlayPause.Foreground = new SolidColorBrush(colorVerde);
                BtnPlayPause.BorderBrush = new SolidColorBrush(colorVerde);
            }
        }

        private void SliderVolumen_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Cambiamos el volumen del archivo en tiempo real según el Slider
            if (audioFile != null)
            {
                audioFile.Volume = (float)SliderVolumen.Value;
            }
        }
        // FUNCIÓN AUTOMÁTICA: Se ejecuta sola cuando NAudio llega al final del MP3
        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // 🔥 ESCUDO: Si el usuario presionó STOP, no avanzamos de canción.
                if (fueDetenidoManualmente)
                {
                    fueDetenidoManualmente = false; // Reseteamos la bandera para la próxima canción
                    return; // Nos salimos sin hacer nada más
                }

                if (estadoRepeticion == 1)
                {
                    ReproducirCancionSeleccionada();
                    return;
                }

                AvanzarSiguienteCancion(esAutomatico: true);
            });
        }

        // LÓGICA DE AVANCE (Siguiente pista)
        private void AvanzarSiguienteCancion(bool esAutomatico)
        {
            if (colaDeReproduccion.Count == 0) return;

            if (modoAleatorio)
            {
                indiceEnColaActual = random.Next(0, colaDeReproduccion.Count);
                ReproducirCancionDesdeCola();
            }
            else
            {
                if (indiceEnColaActual < colaDeReproduccion.Count - 1)
                {
                    indiceEnColaActual++;
                    ReproducirCancionDesdeCola();
                }
                else
                {
                    // Fin de la cola de reproducción actual
                    if (estadoRepeticion == 2) // REPEAT ALL
                    {
                        indiceEnColaActual = 0;
                        ReproducirCancionDesdeCola();
                    }
                    else if (esAutomatico)
                    {
                        // Fin normal sin bucle: Apagamos el reproductor de forma limpia
                        if (outputDevice != null) outputDevice.Stop();
                        ActualizarBotonPlayPause(false);
                        ResetearBarrasVisualizador();
                    }
                }
            }
        }

        // BOTÓN SIGUIENTE (Manual)
        private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
        {
            AvanzarSiguienteCancion(esAutomatico: false);
        }

        // BOTÓN ANTERIOR (Manual)
        private void BtnAnterior_Click(object sender, RoutedEventArgs e)
        {
            if (colaDeReproduccion.Count == 0) return;

            if (modoAleatorio)
            {
                indiceEnColaActual = random.Next(0, colaDeReproduccion.Count);
            }
            else
            {
                if (indiceEnColaActual > 0)
                {
                    indiceEnColaActual--;
                }
                else if (estadoRepeticion == 2) // REPEAT ALL
                {
                    indiceEnColaActual = colaDeReproduccion.Count - 1;
                }
            }
            ReproducirCancionDesdeCola();
        }

        // BOTÓN ALEATORIO (2 Estados: ON / OFF)
        private void BtnAleatorio_Click(object sender, RoutedEventArgs e)
        {
            modoAleatorio = !modoAleatorio; // Cambia el switch
            var convertidor = new System.Windows.Media.ColorConverter();

            if (modoAleatorio)
            {
                BtnAleatorio.Content = "🔀 SHUFFLE [ON]";
                BtnAleatorio.Foreground = new SolidColorBrush((Color)convertidor.ConvertFrom("#00FF66")); // Verde Neón
                BtnAleatorio.BorderBrush = new SolidColorBrush((Color)convertidor.ConvertFrom("#00FF66"));
            }
            else
            {
                BtnAleatorio.Content = "🔀 SHUFFLE [OFF]";
                BtnAleatorio.Foreground = Brushes.White;
                BtnAleatorio.BorderBrush = Brushes.White;
            }
        }

        // BOTÓN REPETICIÓN (3 Estados rotativos: OFF -> ONE -> ALL -> OFF)
        private void BtnRepetir_Click(object sender, RoutedEventArgs e)
        {
            estadoRepeticion = (estadoRepeticion + 1) % 3; // Rota entre 0, 1 y 2
            var convertidor = new System.Windows.Media.ColorConverter();

            switch (estadoRepeticion)
            {
                case 0: // OFF
                    BtnRepetir.Content = "🔁 REPEAT [OFF]";
                    BtnRepetir.Foreground = Brushes.White;
                    BtnRepetir.BorderBrush = Brushes.White;
                    break;

                case 1: // REPETIR UNA CANCIÓN (Fucsia Neón)
                    BtnRepetir.Content = "🔁 REPEAT [ONE]";
                    BtnRepetir.Foreground = new SolidColorBrush((Color)convertidor.ConvertFrom("#FF007F"));
                    BtnRepetir.BorderBrush = new SolidColorBrush((Color)convertidor.ConvertFrom("#FF007F"));
                    break;

                case 2: // REPETIR TODA LA LISTA (Verde Neón)
                    BtnRepetir.Content = "🔁 REPEAT [ALL]";
                    BtnRepetir.Foreground = new SolidColorBrush((Color)convertidor.ConvertFrom("#00FF66"));
                    BtnRepetir.BorderBrush = new SolidColorBrush((Color)convertidor.ConvertFrom("#00FF66"));
                    break;
            }
        }
        // Se ejecuta cada medio segundo de fondo
        private void RelojTiempo_Tick(object sender, EventArgs e)
        {
            // Si la canción está sonando y el usuario NO está haciendo clic sobre la barra...
            if (audioFile != null && !SliderProgreso.IsMouseOver)
            {
                // El reloj actualiza la barra libremente
                SliderProgreso.Value = audioFile.CurrentTime.TotalSeconds;
                TxtTiempoActual.Text = audioFile.CurrentTime.ToString(@"mm\:ss");
            }
        }

        // Se ejecuta APENAS el usuario hace clic en la barra de tiempo
        // Se ejecuta apenas pones el dedo en la barra
        // Se ejecuta apenas tocas o haces clic en la barra
        private void SliderProgreso_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Solo cambiamos el tiempo si el archivo existe y si el dispositivo de salida está activo
            if (audioFile != null && outputDevice != null)
            {
                // 1. Verificamos si el usuario está interactuando con el teclado (foco)
                bool interactuandoConTeclado = SliderProgreso.IsFocused;

                // 2. Verificamos si el usuario hizo clic directo o arrastró el Slider con el mouse.
                // Comprobamos que el mouse esté presionado Y que el puntero esté físicamente sobre el Slider.
                bool interactuandoConMouse = SliderProgreso.IsMouseOver && System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed;

                if (interactuandoConMouse || interactuandoConTeclado)
                {
                    // Teletransportamos el audio inmediatamente con un solo clic o arrastre
                    audioFile.CurrentTime = TimeSpan.FromSeconds(SliderProgreso.Value);
                    TxtTiempoActual.Text = audioFile.CurrentTime.ToString(@"mm\:ss");
                }
            }
        }
        private void ListaCanciones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si la lista se queda vacía por alguna razón, no hacemos nada
            if (ListaCanciones.SelectedIndex < 0) return;
            if (vistaActual != 0 && vistaActual != 4) return;

            // Si el usuario seleccionó un track que NO es el que está sonando actualmente...
            if (ListaCanciones.SelectedIndex != indiceCancionActual)
            {
                // ...el botón debe transformarse en PLAY (Verde) porque al presionarlo cambiará de tema
                ActualizarBotonPlayPause(false);
            }
            else
            {
                // Si el usuario vuelve a hacer clic en la canción que ya está sonando en los parlantes
                if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    // Regresa a PAUSE (Fucsia) porque al presionarlo se pausará la música actual
                    ActualizarBotonPlayPause(true);
                }
            }
        }
        // Se ejecuta constantemente en milisegundos mientras la música avanza
        // Se ejecuta constantemente en milisegundos mientras la música avanza
        private void CanalMuestras_PreVolumeMeter(object sender, StreamVolumeEventArgs e)
        {
            // Usamos el Dispatcher para mover las barras de la interfaz gráfica desde el hilo de audio
            Dispatcher.Invoke(() =>
            {
                if (outputDevice == null || outputDevice.PlaybackState != PlaybackState.Playing)
                {
                    ResetearBarrasVisualizador();
                    return;
                }

                // Se obtiene el valor del volumen en vivo
                float enVivo = e.MaxSampleValues.Length > 0 ? Math.Abs(e.MaxSampleValues[0]) : 0;

                // Multiplicador para exagerar el rebote visual en la pantalla
                float factorEscala = enVivo * 120;

                // Un generador pseudo-aleatorio rítmico basado en el golpe de la canción
                Random r = new Random();

                // Agrupamos los controles en arreglos para poder recorrerlos con un bucle limpio de 0 a 7
                Rectangle[] barras = { Barra0, Barra1, Barra2, Barra3, Barra4, Barra5, Barra6, Barra7 };
                Rectangle[] picos = { Pico0, Pico1, Pico2, Pico3, Pico4, Pico5, Pico6, Pico7 };

                // Multiplicadores rítmicos que ya tenías para cada una de las 8 frecuencias
                double[] multiplicadores = { 1.2, 0.9, 1.1, 0.7, 0.8, 0.6, 1.0, 0.5 };

                const double altoMaximo = 60.0; // Altura máxima física del visualizador (definida en el XAML)
                const double gravedad = 0.8;    // Velocidad con la que caen los picos flotantes

                for (int i = 0; i < 8; i++)
                {
                    // 1. Calculamos la nueva altura usando tu fórmula matemática original
                    double nuevaAltura = factorEscala * multiplicadores[i] * r.NextDouble();

                    // Limitamos los valores para que no rompan el diseño del Grid
                    if (nuevaAltura < 2) nuevaAltura = 2;
                    if (nuevaAltura > altoMaximo) nuevaAltura = altoMaximo;

                    // 2. Cambiamos el color de la barra según el volumen actual (Verde -> Violeta -> Rosa)
                    if (nuevaAltura < altoMaximo * 0.4)
                    {
                        barras[i].Fill = colorBajo;   // Nivel bajo: Verde Neón (#00FF66)
                    }
                    else if (nuevaAltura < altoMaximo * 0.75)
                    {
                        barras[i].Fill = colorMedio;  // Nivel medio: Violeta (#7F00FF)
                    }
                    else
                    {
                        barras[i].Fill = colorAlto;   // Nivel alto/crítico: Rosa Cyberpunk (#FF007F)
                    }

                    // Aplicamos la altura calculada a la barra
                    barras[i].Height = nuevaAltura;

                    // 3. Lógica física de gravedad para los Picos Flotantes (Picos flotando en el aire)
                    double picoActual = picosAlturas[i];

                    if (nuevaAltura >= picoActual)
                    {
                        // Si la barra sube y golpea el pico, lo empuja hacia arriba inmediatamente
                        picoActual = nuevaAltura;
                        picosVelocidadCaida[i] = -0.5; // Pequeño impulso inicial de frenado para que flote antes de caer
                    }
                    else
                    {
                        // Si la barra bajó, el pico cae por efecto de la gravedad acumulada
                        picosVelocidadCaida[i] += gravedad;
                        picoActual -= picosVelocidadCaida[i];

                        // Evitamos que el pico caiga por debajo de la altura de su propia barra de sonido
                        if (picoActual < nuevaAltura)
                        {
                            picoActual = nuevaAltura;
                        }
                    }

                    // Guardamos el estado del pico para el siguiente fotograma
                    picosAlturas[i] = picoActual;

                    // Movemos visualmente el pico horizontal usando su margen inferior
                    picos[i].Margin = new Thickness(0, 0, 0, picoActual);

                    // El color del pico siempre será el rosa neón de máxima alerta para que resalte en el aire
                    picos[i].Fill = colorAlto;
                }
            });
        }

        // Función auxiliar para dejar las barras quietas si le das a STOP o PAUSE
        private void ResetearBarrasVisualizador()
        {
            Barra0.Height = 2; Barra1.Height = 2; Barra2.Height = 2; Barra3.Height = 2;
            Barra4.Height = 2; Barra5.Height = 2; Barra6.Height = 2; Barra7.Height = 2;
        }
        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            // Si estamos en la vista global de Todas (0), Artistas (1) o Álbumes (2), 
            // no hay nada hacia atrás a donde ir.
            if (vistaActual == 0 || vistaActual == 1 || vistaActual == 2) return;

            if (vistaActual == 3)
            {
                // ➔ Estábamos viendo los discos de un artista específico (3).
                // Al volver, regresamos a la lista global de ARTISTAS (1).
                vistaActual = 1;
                ActualizarColoresPestañas(BtnVistaArtistas);

                ListaCanciones.Items.Clear();
                rutasArchivosReales.Clear();

                foreach (var artista in biblioteca.Artistas)
                {
                    ListaCanciones.Items.Add($"[ARTISTA] > {artista.Nombre}");
                }
            }
            else if (vistaActual == 4)
            {
                // ➔ Estábamos viendo las canciones de un disco específico (4).
                // Aquí hay dos variantes: ¿entramos a este disco desde la pestaña Álbumes globales, 
                // o desde adentro de un artista específico?

                if (artistaFiltradoActual != null)
                {
                    // Variación A: Veníamos de explorar un artista. Regresamos a sus DISCOS (3).
                    vistaActual = 3;
                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var album in artistaFiltradoActual.Albumes)
                    {
                        ListaCanciones.Items.Add($"[DISCO] > {album.Nombre}");
                    }
                }
                else
                {
                    // Variación B: Veníamos de la pestaña global de Álbumes. Regresamos a todos los ÁLBUMES (2).
                    vistaActual = 2;
                    ActualizarColoresPestañas(BtnVistaAlbumes);

                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var album in biblioteca.TodosLosAlbumes)
                    {
                        ListaCanciones.Items.Add($"[ÁLBUM] > {album.Nombre} ({album.Artista})");
                    }
                }
            }
            ActualizarVisibilidadVolver();
        }
        private void ActualizarVisibilidadVolver()
        {
            // Si estamos en una subvista (3 o 4), mostramos el botón. Si no, lo ocultamos.
            if (vistaActual == 3 || vistaActual == 4)
            {
                BtnVolver.Visibility = Visibility.Visible;
            }
            else
            {
                BtnVolver.Visibility = Visibility.Collapsed; // No ocupa espacio en la interfaz
            }
        }
        private void BtnEditarMetadatos_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar selección
            if (colaDeReproduccion == null || colaDeReproduccion.Count == 0 || indiceEnColaActual < 0 || indiceEnColaActual >= colaDeReproduccion.Count)
            {
                MessageBox.Show("Por favor, selecciona o reproduce una canción antes de intentar editar sus metadatos.", "Ninguna canción seleccionada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener la ruta del archivo ORIGINAL
            string archivoEditar = colaDeReproduccion[indiceEnColaActual];

            // 3. Validar existencia
            if (!System.IO.File.Exists(archivoEditar))
            {
                MessageBox.Show("El archivo de audio no se encuentra disponible físicamente.", "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. Abrimos el editor directamente (¡SIN DETENER LA MÚSICA!)
            EditorMetadatos editor = new EditorMetadatos(archivoEditar);
            editor.Owner = this;

            // 5. Si el usuario guarda cambios
            if (editor.ShowDialog() == true)
            {
                MessageBox.Show("¡Metadatos actualizados en el archivo original!", "Información", MessageBoxButton.OK, MessageBoxImage.Information);

                // Opcional: Aquí puedes actualizar los textos de la interfaz (título, artista) 
                // si los jalas de los metadatos del archivo original.
                // ActualizarUIConMetadatos(archivoEditar);
            }

            e.Handled = true;
        }
        protected override void OnClosed(EventArgs e)
        {
            if (outputDevice != null) outputDevice.Stop();
            if (audioFile != null) audioFile.Dispose();

            EliminarArchivoTemporal();
            base.OnClosed(e);
        }
    }
}