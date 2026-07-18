using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32; // IMPORTANTE: Usamos el OpenFileDialog nativo de WPF, no el de Forms

namespace CyberPlayer
{
    /// <summary>
    /// Lógica de interacción para EditorMetadatos.xaml
    /// </summary>
    public partial class EditorMetadatos : Window
    {
        private readonly string _archivoPath;
        private byte[]? _nuevaCoverBytes = null;

        // Constructor corregido para aceptar la ruta del archivo que le pasa MainWindow
        public EditorMetadatos(string archivoPath)
        {
            InitializeComponent();
            _archivoPath = archivoPath;
            CargarMetadatos();
        }

        // 1. CARGAR LOS METADATOS DEL ARCHIVO DE AUDIO
        private void CargarMetadatos()
        {
            try
            {
                if (string.IsNullOrEmpty(_archivoPath) || !System.IO.File.Exists(_archivoPath))
                {
                    MessageBox.Show("El archivo de audio no es válido o no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // El 'using' libera el archivo de audio inmediatamente al terminar de leer etiquetas
                using (var file = TagLib.File.Create(_archivoPath))
                {
                    // Cargar textos en los inputs de tu XAML
                    TxtTitulo.Text = file.Tag.Title ?? System.IO.Path.GetFileNameWithoutExtension(_archivoPath);
                    TxtArtista.Text = file.Tag.FirstPerformer ?? "Artista Desconocido";
                    TxtAlbum.Text = file.Tag.Album ?? "Álbum Desconocido";

                    // Cargar imagen de carátula de forma segura sin dejar bloqueos
                    if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                    {
                        var pic = file.Tag.Pictures[0];
                        byte[] imgBytes = pic.Data.Data;

                        // Carga la imagen directamente en memoria (RAM) y cierra el stream de inmediato
                        using (var ms = new System.IO.MemoryStream(imgBytes))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad; // <-- ESTO ES LO CRÍTICO: Fuerza la carga en RAM
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze(); // Hace la imagen inmutable y optimiza el rendimiento en WPF

                            ImgCover.Source = bitmap;
                        }
                    }
                    else
                    {
                        ImgCover.Source = null;
                    }
                } // Físicamente el archivo queda 100% libre en el disco aquí
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar metadatos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 2. BOTÓN: CARGAR PORTADA LOCAL (.jpg, .png)
        private void BtnCargarLocal_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de Imagen (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Seleccionar Carátula"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _nuevaCoverBytes = File.ReadAllBytes(openFileDialog.FileName);
                    ImgCover.Source = BytesToImage(_nuevaCoverBytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo cargar la imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 3. BOTÓN: BUSCAR PORTADA ONLINE
        private void BtnBuscarCover_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Función de búsqueda online lista para implementar.", "Buscar Online", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 4. BOTÓN: GUARDAR CAMBIOS EN EL ARCHIVO
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var file = TagLib.File.Create(_archivoPath))
                {
                    // Guardar textos en las etiquetas
                    file.Tag.Title = TxtTitulo.Text.Trim();
                    file.Tag.Performers = new[] { TxtArtista.Text.Trim() };
                    file.Tag.Album = TxtAlbum.Text.Trim();

                    // Guardar nueva imagen si se seleccionó una
                    if (_nuevaCoverBytes != null)
                    {
                        var nuevaFoto = new TagLib.Picture(new TagLib.ByteVector(_nuevaCoverBytes))
                        {
                            Type = TagLib.PictureType.FrontCover,
                            Description = "Cover",
                            MimeType = "image/jpeg"
                        };
                        file.Tag.Pictures = new TagLib.IPicture[] { nuevaFoto };
                    }

                    file.Save();
                }

                MessageBox.Show("Metadatos guardados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar los cambios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 5. BOTÓN: CANCELAR
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // MÉTODO AUXILIAR: Convertir array de bytes a un objeto Image de WPF
        private BitmapImage BytesToImage(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    } // Aquí es donde debe cerrar la clase realmente
}