using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.ObjectModel;
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
        private AudioFileReader audioFile;

        // Variable global en MainWindow
        private System.IO.FileSystemWatcher monitorCarpetas;

        //Lista oculta en memoria para guardar las rutas reales de los archivos
        private List<string> rutasArchivosReales = new List<string>();
        
        private System.Collections.ObjectModel.ObservableCollection<string> colaDeReproduccion = new System.Collections.ObjectModel.ObservableCollection<string>();
        // El índice real de la canción que está sonando dentro de la cola en memoria
        private int indiceEnColaActual = -1;
        private bool editandoMetadatosActivo = false;
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

        private void BtnVistaArtistas_Click(object sender, RoutedEventArgs e)
        {
            vistaActual = 1; // 1 = Artistas
            ActualizarColoresPestañas(BtnVistaArtistas);

            // OJO: Ya NO tocamos 'colaDeReproduccion' aquí para no romper la música actual.
            // Solo limpiamos la vista visual de la pantalla.
            ListaCanciones.ItemsSource = null;
            ListaCanciones.Items.Clear();
            rutasArchivosReales.Clear();

            // Creamos una lista auxiliar solo para mostrar los artistas en la interfaz
            var listaArtistasVisual = new ObservableCollection<string>();
            foreach (var artista in biblioteca.Artistas)
            {
                listaArtistasVisual.Add($"[ARTISTA] > {artista.Nombre}");
            }

            ListaCanciones.ItemsSource = listaArtistasVisual;
            ActualizarVisibilidadVolver();
        }

        private void BtnVistaAlbumes_Click(object sender, RoutedEventArgs e)
        {
            artistaFiltradoActual = null;
            vistaActual = 2; // 2 = Álbumes
            ActualizarColoresPestañas(BtnVistaAlbumes);

            // Tampoco tocamos 'colaDeReproduccion' aquí.
            ListaCanciones.ItemsSource = null;
            ListaCanciones.Items.Clear();
            rutasArchivosReales.Clear();

            var listaAlbumesVisual = new ObservableCollection<string>();
            foreach (var album in biblioteca.TodosLosAlbumes)
            {
                listaAlbumesVisual.Add($"[ÁLBUM] > {album.Nombre} ({album.Artista})");
            }

            ListaCanciones.ItemsSource = listaAlbumesVisual;
            ActualizarVisibilidadVolver();
        }

        private void MostrarTodasLasCanciones()
        {
            ListaCanciones.ItemsSource = null;
            ListaCanciones.Items.Clear();

            rutasArchivosReales.Clear();

            // Aquí sí cargamos la lista visual de todas las canciones en pantalla
            var listaTodasVisual = new ObservableCollection<string>();

            foreach (var cancion in biblioteca.TodasLasCanciones)
            {
                rutasArchivosReales.Add(cancion.RutaCompleta);
                listaTodasVisual.Add($"{cancion.Artista} - {cancion.Album} > {cancion.Titulo}");
            }

            ListaCanciones.ItemsSource = listaTodasVisual;
        }

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
        // Memoria para recordar qué canciones sonaron en modo Aleatorio
        private Stack<int> historialAtras = new Stack<int>();
        private Stack<int> historialAdelante = new Stack<int>();

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
            // 🛡️ ESCUDO MAESTRO
            if (ListaCanciones.SelectedIndex < 0 || ListaCanciones.SelectedIndex >= rutasArchivosReales.Count)
                return;

            int indiceSeleccionado = ListaCanciones.SelectedIndex;

            // 1. Obtenemos la ruta que el usuario acaba de cliquear
            string rutaArchivoOriginal = rutasArchivosReales[indiceSeleccionado];

            // 2. VALIDACIÓN FÍSICA SILENCIOSA
            // Si hiciste doble clic en una carpeta visual como [ARTISTA] o [ALBUM], esto fallará.
            if (string.IsNullOrEmpty(rutaArchivoOriginal) || !System.IO.File.Exists(rutaArchivoOriginal))
            {
                // Hacemos "return" en silencio. 
                // App seguirá abriendo la carpeta en pantalla, pero la cola de audio
                // no se corrompe y la música de fondo sigue intacta.
                return;
            }

            // 3. SINCRONIZACIÓN BLINDADA
            // Solo si pasó la prueba y es una CANCIÓN REAL, actualizamos la memoria del reproductor.
            colaDeReproduccion = new System.Collections.ObjectModel.ObservableCollection<string>(rutasArchivosReales);

            // Limpiamos la línea temporal porque el usuario forzó una canción a mano
            historialAtras.Clear();
            historialAdelante.Clear();

            // Actualizamos los índices del motor
            indiceEnColaActual = indiceSeleccionado;
            indiceCancionActual = indiceSeleccionado;

            try
            {
                // EXTRAER METADATOS Y CARÁTULA (Sin bloqueos)
                try
                {
                    using (var tagFile = TagLib.File.Create(rutaArchivoOriginal))
                    {
                        string titulo = !string.IsNullOrEmpty(tagFile.Tag.Title)
                            ? tagFile.Tag.Title
                            : System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal);

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
                    TxtTituloActual.Text = System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal).ToUpper();
                    TxtArtistaActual.Text = "ARTISTA DESCONOCIDO";
                    ImgCaratula.Source = null;
                }

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

                EliminarArchivoTemporal();

                try
                {
                    string extension = System.IO.Path.GetExtension(rutaArchivoOriginal);
                    archivoTemporalActivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CyberPlayer_Temp_{Guid.NewGuid()}{extension}");

                    System.IO.File.Copy(rutaArchivoOriginal, archivoTemporalActivo, true);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al preparar el archivo temporal: " + ex.Message);
                    return;
                }

                // 🔥 NAudio ahora lee la COPIA TEMPORAL. El archivo original queda 100% libre.
                audioFile = new AudioFileReader(archivoTemporalActivo);

                canalMuestras = new SampleChannel(audioFile);
                canalMuestras.PreVolumeMeter += CanalMuestras_PreVolumeMeter;

                outputDevice = new WaveOutEvent();
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;

                audioFile.Volume = (float)SliderVolumen.Value;

                outputDevice.Init(canalMuestras);
                outputDevice.Play();

                // Sincronizamos las variables del motor
                indiceCancionActual = indiceSeleccionado;

                // 🛡️ ESCUDO INTERFAZ: Sincronizamos la selección solo si el índice está en el rango actual de la grilla
                if (ListasSonIdenticas(rutasArchivosReales, colaDeReproduccion.ToList()))
                {
                    if (indiceEnColaActual >= 0 && indiceEnColaActual < ListaCanciones.Items.Count)
                    {
                        ListaCanciones.SelectedIndex = indiceEnColaActual;
                    }
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

        private string archivoTemporalActivo = null;

        private void ReproducirCancionDesdeCola()
        {
            // 1. ESCUDO DE SEGURIDAD: Validar rangos básicos de la cola interna
            if (indiceEnColaActual < 0 || indiceEnColaActual >= colaDeReproduccion.Count) return;

            string rutaArchivoOriginal = colaDeReproduccion[indiceEnColaActual];

            // Si la ruta está vacía por una mala transición, salimos silenciosamente
            if (string.IsNullOrEmpty(rutaArchivoOriginal)) return;

            // 🔥 BLINDAJE DEFENSIVO: Si la cadena contiene caracteres visuales de la interfaz ('>') 
            // o el archivo no existe físicamente, rescatamos la ruta real desde 'rutasArchivosReales' automáticamente.
            if (rutaArchivoOriginal.Contains(">") || !System.IO.File.Exists(rutaArchivoOriginal))
            {
                if (rutasArchivosReales != null && indiceEnColaActual >= 0 && indiceEnColaActual < rutasArchivosReales.Count)
                {
                    rutaArchivoOriginal = rutasArchivosReales[indiceEnColaActual];
                    colaDeReproduccion[indiceEnColaActual] = rutaArchivoOriginal; // Auto-repara la cola al vuelo para el próximo "Siguiente"
                }
            }

            // 2. 🌟 VALIDACIÓN FÍSICA FINAL:
            if (!System.IO.File.Exists(rutaArchivoOriginal))
            {
                System.Windows.MessageBox.Show("Ruta fallida: " + rutaArchivoOriginal);
                return;
            }

            try
            {
                // 3. Extraer metadatos con TagLib (Sin bloquear el archivo)
                try
                {
                    using (var tagFile = TagLib.File.Create(rutaArchivoOriginal))
                    {
                        string titulo = !string.IsNullOrEmpty(tagFile.Tag.Title)
                            ? tagFile.Tag.Title
                            : System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal);

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
                        else { ImgCaratula.Source = null; }
                    }
                }
                catch
                {
                    TxtTituloActual.Text = System.IO.Path.GetFileNameWithoutExtension(rutaArchivoOriginal).ToUpper();
                    TxtArtistaActual.Text = "ARTISTA DESCONOCIDO";
                    ImgCaratula.Source = null;
                }

                // 4. Apagar motor viejo de forma limpia y liberar recursos por completo
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

                // 5. Limpiar el archivo temporal de la canción anterior
                EliminarArchivoTemporal();

                // 6. Crear la copia temporal en disco para que NAudio no bloquee el archivo
                try
                {
                    string extension = System.IO.Path.GetExtension(rutaArchivoOriginal);
                    archivoTemporalActivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CyberPlayer_Temp_{Guid.NewGuid()}{extension}");

                    System.IO.File.Copy(rutaArchivoOriginal, archivoTemporalActivo, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error al preparar archivo temporal en cola: " + ex.Message);
                    return;
                }

                // 7. Encender el nuevo track usando la COPIA TEMPORAL
                audioFile = new AudioFileReader(archivoTemporalActivo);
                canalMuestras = new SampleChannel(audioFile);
                canalMuestras.PreVolumeMeter += CanalMuestras_PreVolumeMeter;

                outputDevice = new WaveOutEvent();
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                audioFile.Volume = (float)SliderVolumen.Value;

                outputDevice.Init(canalMuestras);
                outputDevice.Play();

                // 8. 🔄 SINCRONIZACIÓN VISUAL SEGURA
                if (rutasArchivosReales != null && rutasArchivosReales.Contains(rutaArchivoOriginal))
                {
                    int indiceVisualCorrespondiente = rutasArchivosReales.IndexOf(rutaArchivoOriginal);
                    indiceCancionActual = indiceVisualCorrespondiente;

                    if (indiceVisualCorrespondiente >= 0 && indiceVisualCorrespondiente < ListaCanciones.Items.Count)
                    {
                        ListaCanciones.SelectedIndex = indiceVisualCorrespondiente;
                        ListaCanciones.ScrollIntoView(ListaCanciones.Items[indiceVisualCorrespondiente]);
                    }
                }
                else
                {
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
                System.Diagnostics.Debug.WriteLine("Error crítico en cola: " + ex.Message);
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
                // ➔ CASO 0 o 4: Canciones reales. Usamos el cargador inteligente que respeta el Shuffle activo.
                if (rutasArchivosReales != null && seleccionado < rutasArchivosReales.Count)
                {
                    CargarYReproducerCola(rutasArchivosReales, seleccionado);
                }
                else
                {
                    ReproducirCancionSeleccionada();
                }
            }
            else if (vistaActual == 1)
            {
                // ➔ CASO 1: Lista global de ARTISTAS. 
                if (seleccionado < biblioteca.Artistas.Count)
                {
                    artistaFiltradoActual = biblioteca.Artistas[seleccionado];
                    vistaActual = 3;

                    ListaCanciones.ItemsSource = null;
                    ListaCanciones.Items.Clear();
                    rutasArchivosReales.Clear();

                    foreach (var album in artistaFiltradoActual.Albumes)
                    {
                        ListaCanciones.Items.Add($"[DISCO] > {album.Nombre}");
                    }
                }
            }
            else if (vistaActual == 2)
            {
                // ➔ CASO 2: Lista global de ÁLBUMES.
                if (seleccionado < biblioteca.TodosLosAlbumes.Count)
                {
                    albumFiltradoActual = biblioteca.TodosLosAlbumes[seleccionado];
                    vistaActual = 4;

                    ListaCanciones.ItemsSource = null;
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
                // ➔ CASO 3: Discos de un artista específico.
                if (seleccionado < artistaFiltradoActual.Albumes.Count)
                {
                    albumFiltradoActual = artistaFiltradoActual.Albumes[seleccionado];
                    vistaActual = 4;

                    ListaCanciones.ItemsSource = null;
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

                // 🔥 DETENEMOS EL RELOJ PRIMERO para evitar que intente leer posiciones mientras destruimos el motor
                relojTiempo.Stop();

                // 🛠️ LIBERACIÓN TOTAL DEL MOTOR (Así el botón Play puede iniciar limpio de cero)
                if (outputDevice != null)
                {
                    outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped; // Quitamos el evento
                    outputDevice.Stop();
                    outputDevice.Dispose();
                    outputDevice = null;
                }

                if (audioFile != null)
                {
                    audioFile.Dispose();
                    audioFile = null;
                }

                // 🔥 Liberamos el archivo temporal que estaba sonando para no dejar basura en el disco
                EliminarArchivoTemporal();

                // Reseteamos el ecualizador visual y sliders
                ResetearBarrasVisualizador();
                SliderProgreso.Value = 0;
                TxtTiempoActual.Text = "00:00";
                ActualizarBotonPlayPause(false);

                // ===================================================================
                // 🛠️ LIMPIEZA DE METADATOS AL DETENER LA MÚSICA
                // ===================================================================
                TxtTituloActual.Text = "SISTEMA DETENIDO";
                TxtArtistaActual.Text = "SELECCIONA UNA PISTA";
                ImgCaratula.Source = null;
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

                // 🔁 REPEAT ONE: Volvemos a reproducir la misma posición exacta de la cola de reproducción
                if (estadoRepeticion == 1)
                {
                    ReproducirCancionDesdeCola();
                    return;
                }

                AvanzarSiguienteCancion(esAutomatico: true);
            });
        }

        // LÓGICA DE AVANCE (Siguiente pista)
        private void AvanzarSiguienteCancion(bool esAutomatico)
        {
            if (colaDeReproduccion == null || colaDeReproduccion.Count == 0) return;

            // NOTA: Como el modo aleatorio ya mezcló 'colaDeReproduccion' al activarse,
            // avanzar en modo aleatorio o en modo normal es exactamente lo mismo: 
            // pasar secuencialmente al siguiente índice de la cola.

            if (indiceEnColaActual < colaDeReproduccion.Count - 1)
            {
                // Guardamos la posición actual en el historial de retroceso antes de avanzar
                historialAtras.Push(indiceEnColaActual);
                historialAdelante.Clear(); // Limpiamos el futuro al avanzar

                indiceEnColaActual++;
                ReproducirCancionDesdeCola();
            }
            else
            {
                // Hemos llegado a la última canción de la cola (ya sea en orden natural o en orden aleatorio)
                if (estadoRepeticion == 2) // Repetir toda la lista
                {
                    historialAtras.Push(indiceEnColaActual);
                    historialAdelante.Clear();
                    indiceEnColaActual = 0; // Regresa al inicio de la cola
                    ReproducirCancionDesdeCola();
                }
                else
                {
                    // Fin de la cola: la última canción ha terminado (o se presionó siguiente en la última)
                    if (outputDevice != null) outputDevice.Stop();
                    ActualizarBotonPlayPause(false);
                    ResetearBarrasVisualizador();
                    TxtTituloActual.Text = "FIN DE LA COLA";
                    TxtArtistaActual.Text = "SELECCIONA OTRA PISTA";
                    ImgCaratula.Source = null;
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
            if (colaDeReproduccion == null || colaDeReproduccion.Count == 0) return;

            if (modoAleatorio)
            {
                if (historialAtras.Count > 0)
                {
                    // 🔥 Guardamos la canción actual en el "futuro" antes de retroceder
                    historialAdelante.Push(indiceEnColaActual);

                    // Ahora sí, volvemos a la canción anterior
                    indiceEnColaActual = historialAtras.Pop();
                }
                else
                {
                    indiceEnColaActual = 0;
                }
            }
            else
            {
                // ... (Tu código original lineal sigue exactamente igual)
                if (indiceEnColaActual > 0)
                {
                    indiceEnColaActual--;
                }
                else if (estadoRepeticion == 2)
                {
                    indiceEnColaActual = colaDeReproduccion.Count - 1;
                }
                else
                {
                    indiceEnColaActual = 0;
                }
            }

            indiceCancionActual = indiceEnColaActual;
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

                // 🌟 LÓGICA DE ACTIVACIÓN: Reorganizar la cola manteniendo la canción actual como ancla
                if (colaDeReproduccion != null && colaDeReproduccion.Count > 0 && indiceEnColaActual >= 0 && indiceEnColaActual < colaDeReproduccion.Count)
                {
                    string cancionActual = colaDeReproduccion[indiceEnColaActual];

                    // CORRECCIÓN: Evitamos el conflicto de tipos evaluando de forma segura
                    List<string> listaBase;
                    if (rutasArchivosReales != null && rutasArchivosReales.Count > 0)
                    {
                        listaBase = new List<string>(rutasArchivosReales);
                    }
                    else
                    {
                        listaBase = new List<string>(colaDeReproduccion);
                    }

                    // Quitamos temporalmente la canción actual para que no se repita al azar en la mezcla
                    listaBase.Remove(cancionActual);

                    // Barajamos el resto de las canciones (Algoritmo Fisher-Yates)
                    var rnd = new Random();
                    int n = listaBase.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = rnd.Next(n + 1);
                        string value = listaBase[k];
                        listaBase[k] = listaBase[n];
                        listaBase[n] = value;
                    }

                    // Insertamos la canción actual estrictamente al inicio de la nueva mezcla aleatoria
                    listaBase.Insert(0, cancionActual);

                    // Actualizamos la ObservableCollection de la cola sin romper el enlace visual
                    colaDeReproduccion.Clear();
                    foreach (var ruta in listaBase)
                    {
                        colaDeReproduccion.Add(ruta);
                    }

                    // Fijamos el índice actual en 0 (la canción que suena pasa a ser el inicio de este ciclo aleatorio)
                    indiceEnColaActual = 0;
                }
            }
            else
            {
                BtnAleatorio.Content = "🔀 SHUFFLE [OFF]";
                BtnAleatorio.Foreground = Brushes.White;
                BtnAleatorio.BorderBrush = Brushes.White;

                // 🌟 LÓGICA DE DESACTIVACIÓN: Volver al orden original sin perdernos de la canción actual
                if (colaDeReproduccion != null && colaDeReproduccion.Count > 0 && indiceEnColaActual >= 0 && indiceEnColaActual < colaDeReproduccion.Count)
                {
                    string cancionActual = colaDeReproduccion[indiceEnColaActual];

                    if (rutasArchivosReales != null && rutasArchivosReales.Contains(cancionActual))
                    {
                        int indexOriginal = rutasArchivosReales.IndexOf(cancionActual);

                        // Restauramos la cola al orden original del disco/carpeta
                        colaDeReproduccion.Clear();
                        foreach (var ruta in rutasArchivosReales)
                        {
                            colaDeReproduccion.Add(ruta);
                        }

                        // Posicionamos el índice exactamente donde le corresponde en el orden original
                        indiceEnColaActual = indexOriginal;
                    }
                }
            }

            // Limpiamos el historial de navegación al alternar el modo aleatorio para evitar conflictos de índices
            historialAtras.Clear();
            historialAdelante.Clear();
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
                    BtnRepetir.BorderBrush = new SolidColorBrush((Color)convertidor.ConvertFrom("#00FF66"));
                    break;
            }
        }
        private void RelojTiempo_Tick(object sender, EventArgs e)
        {
            // 1. Si la variable ya es null de entrada, apagamos el reloj
            if (audioFile == null)
            {
                relojTiempo.Stop();
                return;
            }

            try
            {
                if (!SliderProgreso.IsMouseOver)
                {
                    // 2. Usamos el operador '?' para protegernos. 
                    // Si NAudio destruye el objeto internamente en este milisegundo, 'tiempo' será null en vez de crashear.
                    var tiempo = audioFile?.CurrentTime;

                    if (tiempo.HasValue)
                    {
                        SliderProgreso.Value = tiempo.Value.TotalSeconds;
                        TxtTiempoActual.Text = tiempo.Value.ToString(@"mm\:ss");
                    }
                }
            }
            catch
            {
                // Absorción total en el cierre
                relojTiempo.Stop();
            }
        }

        // Se ejecuta apenas tocas o haces clic en la barra
        private void SliderProgreso_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Solo cambiamos el tiempo si el archivo existe y si el dispositivo de salida está activo
            if (audioFile != null && outputDevice != null)
            {
                // 1. Verificamos si el usuario está interactuando con el teclado (foco)
                bool interactuandoConTeclado = SliderProgreso.IsFocused;

                // 2. Verificamos si el usuario hizo clic directo o arrastró el Slider con el mouse.
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

                // Multiplicadores rítmicos para cada una de las 8 frecuencias
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

                    // 3. Lógica física de gravedad para los Picos Flotantes
                    double picoActual = picosAlturas[i];

                    if (nuevaAltura >= picoActual)
                    {
                        picoActual = nuevaAltura;
                        picosVelocidadCaida[i] = -0.5; // Pequeño impulso inicial de frenado
                    }
                    else
                    {
                        picosVelocidadCaida[i] += gravedad;
                        picoActual -= picosVelocidadCaida[i];

                        if (picoActual < nuevaAltura)
                        {
                            picoActual = nuevaAltura;
                        }
                    }

                    // Guardamos el estado del pico para el siguiente fotograma
                    picosAlturas[i] = picoActual;

                    // Movemos visualmente el pico horizontal usando su margen inferior
                    picos[i].Margin = new Thickness(0, 0, 0, picoActual);

                    // El color del pico siempre será el rosa neón de máxima alerta
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
            if (vistaActual == 0 || vistaActual == 1 || vistaActual == 2) return;

            if (vistaActual == 3)
            {
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
                if (artistaFiltradoActual != null)
                {
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
            if (vistaActual == 3 || vistaActual == 4)
            {
                BtnVolver.Visibility = Visibility.Visible;
            }
            else
            {
                BtnVolver.Visibility = Visibility.Collapsed;
            }
        }
        private void BtnEditarMetadatos_Click(object sender, RoutedEventArgs e)
        {
            if (ListaCanciones.SelectedIndex < 0)
            {
                MessageBox.Show("Por favor, selecciona una canción de la lista antes de intentar editar sus metadatos.", "Ninguna canción seleccionada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int indexVisual = ListaCanciones.SelectedIndex;
            string archivoEditar = "";

            if (vistaActual == 0 || vistaActual == 4)
            {
                if (rutasArchivosReales != null && indexVisual < rutasArchivosReales.Count)
                {
                    archivoEditar = rutasArchivosReales[indexVisual];
                }
            }

            if (string.IsNullOrEmpty(archivoEditar) && indiceEnColaActual >= 0 && indiceEnColaActual < colaDeReproduccion.Count)
            {
                archivoEditar = colaDeReproduccion[indiceEnColaActual];
            }

            if (!string.IsNullOrEmpty(archivoEditar) && (archivoEditar.Contains(">") || archivoEditar.Contains(" - ")))
            {
                if (rutasArchivosReales != null && indexVisual < rutasArchivosReales.Count)
                {
                    archivoEditar = rutasArchivosReales[indexVisual];
                }
            }

            if (string.IsNullOrEmpty(archivoEditar) || !System.IO.File.Exists(archivoEditar))
            {
                MessageBox.Show("El archivo de audio no se encuentra disponible físicamente o la vista actual no permite edición.", "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ESCUDO 1: Apagamos el monitor de carpetas temporalmente.
            // Evita que el FileSystemWatcher y el Editor peleen por leer/escribir el mismo archivo.
            if (monitorCarpetas != null) monitorCarpetas.EnableRaisingEvents = false;

            EditorMetadatos editor = new EditorMetadatos(archivoEditar);
            editor.Owner = this;

            if (editor.ShowDialog() == true)
            {
                RefrescarElementoModificado(archivoEditar);
            }

            // Volvemos a encender el radar una vez que el archivo fue guardado y cerrado con seguridad.
            if (monitorCarpetas != null) monitorCarpetas.EnableRaisingEvents = true;

            e.Handled = true;
        }
        protected override void OnClosed(EventArgs e)
        {
            if (outputDevice != null) outputDevice.Stop();
            if (audioFile != null) audioFile.Dispose();

            EliminarArchivoTemporal();
            base.OnClosed(e);
        }
        private void ActivarMonitoreoCarpeta(string rutaCarpeta)
        {
            if (monitorCarpetas != null)
            {
                monitorCarpetas.EnableRaisingEvents = false;
                monitorCarpetas.Dispose();
            }

            monitorCarpetas = new System.IO.FileSystemWatcher();
            monitorCarpetas.Path = rutaCarpeta;
            monitorCarpetas.Filter = "*.mp3";
            monitorCarpetas.IncludeSubdirectories = true;
            monitorCarpetas.NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.LastWrite;

            monitorCarpetas.Changed += MonitorCarpetas_Changed;
            monitorCarpetas.Renamed += MonitorCarpetas_Renamed;

            monitorCarpetas.EnableRaisingEvents = true;
        }

        private void MonitorCarpetas_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            string rutaArchivoModificado = e.FullPath;

            // ESCUDO 2: Retraso asíncrono.
            // Cuando programas externos modifican un MP3, el sistema operativo retiene el archivo una fracción de segundo.
            // Darle medio segundo asegura que TagLib no se estrelle intentando leer un archivo que aún se está escribiendo.
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
            {
                RefrescarElementoModificado(rutaArchivoModificado);
            });
        }

        private void MonitorCarpetas_Renamed(object sender, System.IO.RenamedEventArgs e)
        {
            string rutaVieja = e.OldFullPath;
            string rutaNueva = e.FullPath;

            this.Dispatcher.Invoke(() =>
            {
                if (rutasArchivosReales != null && rutasArchivosReales.Contains(rutaVieja))
                {
                    int index = rutasArchivosReales.IndexOf(rutaVieja);
                    rutasArchivosReales[index] = rutaNueva;
                }

                if (colaDeReproduccion != null && colaDeReproduccion.Contains(rutaVieja))
                {
                    int indexCola = colaDeReproduccion.IndexOf(rutaVieja);
                    colaDeReproduccion[indexCola] = rutaNueva;
                }

                RefrescarElementoModificado(rutaNueva);
            });
        }

        private void RefrescarElementoModificado(string rutaArchivo)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(rutaArchivo)) return;

                    // ESCUDO 3: Encontrar el índice REAL en la lista, NO el que el usuario tiene clickeado.
                    // Esto evita corromper la interfaz si editas una canción en Windows mientras reproduces otra en la app.
                    int indexReal = -1;
                    if (rutasArchivosReales != null && rutasArchivosReales.Contains(rutaArchivo))
                    {
                        indexReal = rutasArchivosReales.IndexOf(rutaArchivo);
                    }

                    // 2. Leer metadatos frescos desde el disco duro
                    using (var tagFile = TagLib.File.Create(rutaArchivo))
                    {
                        string tituloNuevo = !string.IsNullOrEmpty(tagFile.Tag.Title)
                            ? tagFile.Tag.Title
                            : System.IO.Path.GetFileNameWithoutExtension(rutaArchivo);

                        string artistaNuevo = (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0)
                            ? tagFile.Tag.Performers[0]
                            : "Artista Desconocido";

                        string albumNuevo = tagFile.Tag.Album ?? "Álbum Desconocido";

                        // 3. Buscar en la cola de reproducción actual
                        int indexInterno = -1;
                        if (colaDeReproduccion != null && colaDeReproduccion.Contains(rutaArchivo))
                            indexInterno = colaDeReproduccion.IndexOf(rutaArchivo);

                        // 4. Si es la canción que está sonando AHORA, actualizamos los textos inferiores
                        if (indexInterno != -1 && (indexInterno == indiceEnColaActual || indexInterno == indiceCancionActual))
                        {
                            TxtTituloActual.Text = tituloNuevo.ToUpper();
                            TxtArtistaActual.Text = artistaNuevo.ToUpper();
                        }

                        // 5. ACTUALIZACIÓN DE LA GRILLA (Solo si la canción pertenece a la vista actual en pantalla)
                        if (indexReal != -1)
                        {
                            string textoVisualNuevo = $"{artistaNuevo} - {albumNuevo} > {tituloNuevo}";

                            if (ListaCanciones.ItemsSource != null)
                            {
                                if (ListaCanciones.ItemsSource is System.Collections.IList listaEnMemoria)
                                {
                                    if (indexReal >= 0 && indexReal < listaEnMemoria.Count)
                                    {
                                        listaEnMemoria[indexReal] = textoVisualNuevo;
                                    }
                                }
                            }
                            else
                            {
                                if (indexReal >= 0 && indexReal < ListaCanciones.Items.Count)
                                {
                                    ListaCanciones.Items[indexReal] = textoVisualNuevo;
                                }
                            }

                            // ESCUDO 4: Evitar el cartel de WPF (InvalidOperationException).
                            // Solo disparamos el Refresh() nativo si NO estamos usando un Binding (ItemsSource).
                            if (ListaCanciones.ItemsSource == null)
                            {
                                ListaCanciones.Items.Refresh();
                            }

                            // Ya no robamos el foco con SelectedIndex. Si el archivo se actualiza en segundo plano, 
                            // no te interrumpirá visualmente lo que estés haciendo.
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error controlado al refrescar grilla: {ex.Message}");
                }
            });
        }

        // Método de soporte discreto para validar si es la canción activa sin romper el hilo
        private bool indexVisualValidado(string ruta)
        {
            try
            {
                return colaDeReproduccion != null &&
                       indiceEnColaActual < colaDeReproduccion.Count &&
                       colaDeReproduccion[indiceEnColaActual] == ruta;
            }
            catch { return false; }
        }
        private void CargarYReproducerCola(List<string> rutasOriginales, int indiceSeleccionado)
        {
            if (rutasOriginales == null || rutasOriginales.Count == 0 || indiceSeleccionado < 0 || indiceSeleccionado >= rutasOriginales.Count)
                return;

            colaDeReproduccion.Clear();

            if (modoAleatorio)
            {
                // Si el aleatorio ya está activo, barajamos la lista inmediatamente 
                // colocando la canción seleccionada como la ancla principal (índice 0).
                string cancionElegida = rutasOriginales[indiceSeleccionado];
                var listaBase = new List<string>(rutasOriginales);
                listaBase.Remove(cancionElegida);

                var rnd = new Random();
                int n = listaBase.Count;
                while (n > 1)
                {
                    n--;
                    int k = rnd.Next(n + 1);
                    string value = listaBase[k];
                    listaBase[k] = listaBase[n];
                    listaBase[n] = value;
                }

                listaBase.Insert(0, cancionElegida);

                foreach (var ruta in listaBase)
                {
                    colaDeReproduccion.Add(ruta);
                }
                indiceEnColaActual = 0; // La canción elegida arranca al inicio del ciclo aleatorio
            }
            else
            {
                // Si está apagado, se carga en orden secuencial normal
                foreach (var ruta in rutasOriginales)
                {
                    colaDeReproduccion.Add(ruta);
                }
                indiceEnColaActual = indiceSeleccionado;
            }

            ReproducirCancionDesdeCola();
        }
    }
}