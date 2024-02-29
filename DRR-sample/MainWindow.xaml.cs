
using EvilDICOM.RT.Data.DVH;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Reconstruction;
using Kitware.VTK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static Kitware.VTK.vtkAxis;
using Color = System.Windows.Media.Color;

namespace DRR_sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int[] dimensions;
        public double[] spacing;
        public float[] Origin;
        //string dicomDirectory = @"D:\DicomImagesWithoutRTS\A990428-THYMOMA2\Study_1";
        //string dicomDirectory = @"D:\DicomImagesWithoutRTS\A990515_ABDOLAHI-AZAM\Study_1";
        string dicomDirectory = @"C:\Users\k703528\Documents\Study_1 _ CompleteS";
        private ObservableCollection<vtkMatrix4x4> matrixRotations = new();
        

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowsFormsHost_Loaded(object sender, RoutedEventArgs e)
        {
            //renderWindowControl.RenderWindow.Render();
            //renderWindowControl.RenderWindow.GetInteractor().SetInteractorStyle(vtkInteractorStyleImage.New());
            //display3DWithVolume();
            //Reslice3DLayers();
            initializeImagedata();

            //filterIsosurface();
            //filterAndDrr();

            /*3D body*/
            //setDicom();
        }
        
        private void initializeImagedata()
        {
            // Read the DICOM directory
            vtkDICOMImageReader reader = vtkDICOMImageReader.New();
            reader.SetDirectoryName(dicomDirectory);
            reader.Update();
            var origin = reader.GetImagePositionPatient();

            vtkImageData imageData = reader.GetOutput();
            var center = imageData.GetCenter();
            imageData.SetOrigin(-center[0], -center[1], -center[2]);//Move Images center into (0,0,0)

            var spacing = imageData.GetSpacing();
            var extend = imageData.GetExtent();

            DRRrotation(transformeImageDataWithReslice(imageData));
           
        }

        private void DRRrotation(vtkImageData rotatedImageData)
        {
            
            int slabNumberOfSlices = rotatedImageData.GetDimensions()[0] * (int)rotatedImageData.GetSpacing()[2];

            // Convert the DICOM images to DRR
            vtkImageReslice reslice = vtkImageReslice.New();
            reslice.SetInputData(rotatedImageData);
            reslice.SetOutputDimensionality(2);
            reslice.SlabTrapezoidIntegrationOn();
            reslice.SetSlabSliceSpacingFraction(1);
            reslice.InterpolateOn();
            reslice.SetSlabModeToMax();
            reslice.SetSlabNumberOfSlices(slabNumberOfSlices);
            //reslice.SetInterpolationModeToLinear();
            //Axis Rotation
            vtkTransform transformAxis = new();
            transformAxis.RotateX(-90);
            //reslice.SetResliceAxes(transformAxis.GetMatrix());

            reslice.Update();
            //---------1-----------
            //visualizeDrr(reslice.GetOutput());
            //filterDrrWithThrashould(reslice.GetOutput());
            //---------2------------
            //filterDrrWithVolume(reslice.GetOutput());
            filterDrrWithVolume(rotatedImageData);
            //filterDrrWithLookupTable(reslice.GetOutput());
            //---------3--------------
            //filterDrrWithLookupTable(reslice.GetOutput());
            //marchingubeTest(reslice.GetOutput());
            //marchingubeTest(rotatedImageData);

        }

        private void filterDrrWithVolume(vtkImageData drrimage)
        {
            double fatHU = -200;
            double airHU = -1024;
            double softTissueHU = -24;
            double boneHU = 200;
            double metalHU = 776;
            
            //--------------mapper 2----------------
            vtkFixedPointVolumeRayCastMapper raycastVolumeMapper = vtkFixedPointVolumeRayCastMapper.New();
            raycastVolumeMapper.SetInputData(drrimage);
            //--------------mapper 1 ---------------
            var volumeMapper = vtkSmartVolumeMapper.New();
            volumeMapper.SetInputData(drrimage);
            volumeMapper.SetBlendModeToComposite();
            volumeMapper.Update();
            //--------------mapper 3-----------------
            vtkAbstractVolumeMapper mapper;
            mapper = vtkFixedPointVolumeRayCastMapper.New();
            mapper.SetInputDataObject(drrimage);
            //--------------Opacity-------------------
            var opacityTransferFunction = vtkPiecewiseFunction.New();
            opacityTransferFunction.AddPoint(airHU, 0); // Air: Completely transparent
            opacityTransferFunction.AddPoint(-999, 0); // Air: Completely transparent
            opacityTransferFunction.AddPoint(fatHU, 0.01); // Fat: Slightly more opaque
            opacityTransferFunction.AddPoint(softTissueHU, 0.01); // Soft Tissue: Visible
            opacityTransferFunction.AddPoint(100, 0.1); // Soft Tissue: Visible
            opacityTransferFunction.AddPoint(boneHU, .5); // Bone: Highly opaque
            opacityTransferFunction.AddPoint(metalHU, .7); // Bone: Highly opaque

            //----------------Color------------
            var colorTransferFunction = vtkColorTransferFunction.New();
            colorTransferFunction.AddRGBPoint(airHU, 0.0, 0.0, 0.0); // Air: Black
            colorTransferFunction.AddRGBPoint(fatHU, 1, 1, 1); // Fat: Dark Gray
            colorTransferFunction.AddRGBPoint(softTissueHU, 1, 1, 1); // Soft Tissue: Lighter Gray
            colorTransferFunction.AddRGBPoint(100, 1, 1, 1); // Soft Tissue: Lighter Gray
            colorTransferFunction.AddRGBPoint(boneHU, 1.0, 1.0, 1.0); // Bone: White

             
            var volumeProperty = vtkVolumeProperty.New();
            volumeProperty.SetColor(colorTransferFunction);
            volumeProperty.SetScalarOpacity(opacityTransferFunction);
            //volumeProperty.SetInterpolationTypeToLinear(); // Sets the interpolation type (linear is common)
            //............. more realistic............
            volumeProperty.ShadeOn();
            volumeProperty.SetAmbient(0.1);  // Ambient light coefficient
            volumeProperty.SetDiffuse(0.9);  // Diffuse light coefficient
            volumeProperty.SetSpecular(0.2); // Specular light coefficient
            volumeProperty.SetSpecularPower(10); // Specular power (sharpness of specular reflection)

            var gradientOpacity = vtkPiecewiseFunction.New();
            gradientOpacity.AddPoint(0, 0.0);  // Low gradient: Transparent
            gradientOpacity.AddPoint(90, 1.0); // High gradient: Opaque
            volumeProperty.SetGradientOpacity(gradientOpacity);

            var volume = vtkVolume.New();
            volume.SetMapper(volumeMapper);
            //volume.SetMapper(raycastVolumeMapper);
            //volume.SetMapper(mapper);
            volume.SetProperty(volumeProperty);

            vtkImageReslice resliceValume= vtkImageReslice.New();
            //resliceValume.SetInputData(volume);
            //resliceValume.SetOutputDimensionality(2);
            //resliceValume.SlabTrapezoidIntegrationOn();
            //resliceValume.SetSlabSliceSpacingFraction(1);
            //resliceValume.InterpolateOn();
            //resliceValume.SetSlabModeToMax();
            //resliceValume.SetSlabNumberOfSlices(1000);
            resliceValume.Update();

            var renderWindow = renderWindowControl.RenderWindow;

            vtkRenderWindowInteractor renderWindowInteractor = vtkRenderWindowInteractor.New();
            renderWindowInteractor.SetRenderWindow(renderWindow);

            renderWindow.GetRenderers().GetFirstRenderer().AddVolume(volume);
            //renderWindow.GetRenderers().GetFirstRenderer().SetBackground(1,1,1);
            renderWindow.GetRenderers().GetFirstRenderer().ResetCamera();
            renderWindowInteractor.Start();

            
            //vtkWindowToImageFilter windowToImageFilter = vtkWindowToImageFilter.New();
            //windowToImageFilter.SetInput(renderWindow);
            //windowToImageFilter.Update();
            //vtkImageData outputsindow = windowToImageFilter.GetOutput();
            //vtkImageActor imageActor = vtkImageActor.New();
            //imageActor.SetInputData(outputsindow);
            //renderWindow.GetRenderers().GetFirstRenderer().AddActor(imageActor);
            //vtkRenderWindowInteractor imagerenderWindowInteractor = vtkRenderWindowInteractor.New();
            //imagerenderWindowInteractor.SetRenderWindow(renderWindow);
            //imagerenderWindowInteractor.Start();
        }

        private vtkImageData transformeImageDataWithReslice(vtkImageData imageData)
        {
            //Rotate ImageData
            vtkTransform transform = new();
            transform.RotateX(-90);
            
            vtkImageReslice reslice = new();
            reslice.SetInputData(imageData);
            reslice.SetResliceTransform(transform);
            reslice.SetResliceAxesOrigin(0,0,0);// Origin of the slice plane (can be adjusted based on your needs)
            reslice.Update();


            vtkImageData transformedImageData = reslice.GetOutput();
            return transformedImageData;
        }

        /// <summary>
        /// Test Area
        /// </summary>
        private void setDicom()
        {
            vtkDICOMImageReader reader = vtkDICOMImageReader.New();
            reader.SetDirectoryName(dicomDirectory);
            reader.Update();
            Display3D(reader);
        }
        private void Display3D(vtkDICOMImageReader readDico)
        {

            vtkAbstractVolumeMapper mapper;
            mapper = vtkFixedPointVolumeRayCastMapper.New();
            mapper.SetInputConnection(readDico.GetOutputPort());
            
            /*vtkVolumeRayCastMapper mapper = vtkVolumeRayCastMapper.New();
            mapper.SetInputData(imageData);*/

            vtkVolumeProperty property = vtkVolumeProperty.New();
            // Set transfer functions and other properties as needed
            vtkPiecewiseFunction opacityTF = vtkPiecewiseFunction.New();
            opacityTF.AddPoint(-200, 0.0); // Set low opacity for air
            opacityTF.AddPoint(3000, 1.0); // Set high opacity for bone
            opacityTF.AddPoint(200, 0.9); // Set high opacity for bone start
            opacityTF.AddPoint(100, 0.05); // Set medium opacity for soft tissue
            //opacityTF.AddPoint(400, 1.0); // Set high opacity for high-density structures

            vtkColorTransferFunction colorTF = vtkColorTransferFunction.New();
            //colorTF.AddRGBPoint(-200, 0.3, 0.3, 0.3); // color for air/bone
            //colorTF.AddRGBPoint(-900, 0.3, 0.3, 0.3); // color for air
            colorTF.AddRGBPoint(-700, 0.5, 0.1, 0); 
            colorTF.AddRGBPoint(100, 0.9, 0.9, 0.9); // color of soft tissue
            colorTF.AddRGBPoint(200, .8, 0.8, .8); 
            //colorTF.AddRGBPoint(50, .8, 0, 0); 
            colorTF.AddRGBPoint(400, 0, 1.0, 0); // Blue for high-density structures
            colorTF.AddRGBPoint(3000, 1,0,0);
            
            property.SetColor(colorTF);
            property.SetScalarOpacity(opacityTF);

            vtkVolume volume = vtkVolume.New();
            volume.SetMapper(mapper);
            volume.SetProperty(property);

            var renderWindow = renderWindowControl.RenderWindow;

            renderWindow.GetRenderers().GetFirstRenderer().AddVolume(volume);
            //renderWindow.GetRenderers().GetFirstRenderer().SetBackground(1,1,1);

            
            renderWindow.AddRenderer(renderWindow.GetRenderers().GetFirstRenderer());

            vtkRenderWindowInteractor interactor = vtkRenderWindowInteractor.New();
            interactor.SetRenderWindow(renderWindow);

            renderWindow.Render();
            interactor.Start();
        }

        private void display3DWithVolume()
        {
            var renderWindow = renderWindowControl.RenderWindow;
            vtkDICOMImageReader dicomReader = vtkDICOMImageReader.New();
            dicomReader.SetDirectoryName(dicomDirectory);

            // Step 3: Create a 3D volume mapper and actor
            vtkFixedPointVolumeRayCastMapper volumeMapper = vtkFixedPointVolumeRayCastMapper.New();
            volumeMapper.SetInputConnection(dicomReader.GetOutputPort());

            vtkVolume volume = vtkVolume.New();
            volume.SetMapper(volumeMapper);

           
            renderWindow.AddRenderer(renderWindow.GetRenderers().GetFirstRenderer());

            // Step 5: Set up a render window interactor
            vtkRenderWindowInteractor renderWindowInteractor = vtkRenderWindowInteractor.New();
            renderWindowInteractor.SetRenderWindow(renderWindow);

            // Add volume to the renderer
            renderWindow.GetRenderers().GetFirstRenderer().AddVolume(volume);
            renderWindow.GetRenderers().GetFirstRenderer().ResetCamera();
            renderWindow.GetRenderers().GetFirstRenderer().SetBackground(1,1,1);

            // Set up render window size
            renderWindow.SetSize(800, 600);

            // Start the rendering loop
            renderWindow.Render();
            renderWindowInteractor.Start();
        }

        private void Reslice3DLayers()
        {
            // Create a DICOM reader
            vtkDICOMImageReader dicomReader = vtkDICOMImageReader.New();
            dicomReader.SetDirectoryName(dicomDirectory);
            dicomReader.Update();

            // Get the output from the DICOM reader
            vtkImageData dicomImage = dicomReader.GetOutput();
            var center = dicomImage.GetCenter();
            dicomImage.SetOrigin(-center[0], -center[1], -center[2]);//Move Images center into (0,0,0)
            var imagetransfered = transformeImageDataWithReslice(dicomImage);
            //var imagetransfered = transformeImageData(dicomImage);

            var spacing = dicomImage.GetSpacing();
            var extend = dicomImage.GetExtent();

            // Create an image filter for generating the DRR
            vtkImageReslice imageReslice = vtkImageReslice.New();
            //imageReslice.SetInputData(imagetransfered);
            imageReslice.SetInputData(dicomImage);
            //imageReslice.SetResliceAxesDirectionCosines(1, 1, 0, 0, 0, 1, 0, 1, 0); // side
            //imageReslice.SetResliceAxesDirectionCosines(0, 0, 1, 1, -1, 0, 0, 1, 0);//side
            //imageReslice.SetResliceAxesDirectionCosines(0, 0, -1, 0, 1, 0, 1, 0, 0);// Sagittal plane
            //imageReslice.SetResliceAxesDirectionCosines(1, 0, 0, 0, 0, -1, 0, 1, 0); // Coronal plane
            imageReslice.SetInterpolationModeToLinear();

            // Set up the extent and spacing
            imageReslice.SetOutputSpacing(spacing[0], spacing[1], spacing[2]);
            imageReslice.SetOutputExtent(extend[0], extend[1], extend[2], extend[3], extend[4], extend[5]);

            // Update the DRR filter
            imageReslice.Update();

            // Get the output 3D reslice image
            vtkImageData drrImage = imageReslice.GetOutput();

            //Display all slices
            // Get image dimensions
            int[] dims = dicomImage.GetDimensions();
            int numSlices = dims[2];
            int centerSlice = numSlices / 2;


            renderWindowControl.RenderWindow.AddRenderer(renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer());

            // Create an image actor for each slice and add it to the renderer
            //for (int i = numSlices-1; i >0; i-=5)
            for (int i = 15; i< numSlices-40; i+=5)
            //for (int i = centerSlice; i< centerSlice+1; i++)
            {
                vtkImageActor act = vtkImageActor.New();
                act.SetInputData(drrImage);
                act.SetDisplayExtent(0, dims[0] - 1, 0, dims[1] - 1, i, i); // Display only slice i
                renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(act);
                act.Dispose();
            }

            vtkImageActor imageActor = new();
            imageActor.SetInputData(drrImage);
            //renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(imageActor);
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().SetBackground(1,1,1);
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().GetActiveCamera().ParallelProjectionOn();
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().ResetCamera();

            // Clean up
            dicomReader.Dispose();
            imageReslice.Dispose();
            
           
        }

        private void filterIsosurface()
        {
            double fatHU = -274;
            double airHU = -999;
            double softTissueHU = -24;
            double boneHU = 176;
            double metalHU = 776;
            vtkDICOMImageReader reader = vtkDICOMImageReader.New();
            reader.SetDirectoryName(dicomDirectory);
            reader.Update();

            var origin = reader.GetImagePositionPatient();

            vtkImageData imageData = reader.GetOutput();
            var center = imageData.GetCenter();
            imageData.SetOrigin(-center[0], -center[1], -center[2]);

            //// Step 2: Segment bone
            //vtkImageThreshold boneThresholdFilter = vtkImageThreshold.New();
            //boneThresholdFilter.SetInputData(imageData);
            //boneThresholdFilter.ThresholdByUpper(200); // Adjust threshold as per your image
            //boneThresholdFilter.ReplaceInOn();
            //boneThresholdFilter.SetInValue(255);
            //boneThresholdFilter.SetOutValue(0);
            //boneThresholdFilter.Update();

            //// Step 3: Segment skin
            //vtkImageThreshold skinThresholdFilter = vtkImageThreshold.New();
            //skinThresholdFilter.SetInputData(imageData);
            //skinThresholdFilter.ThresholdBetween(softTissueHU, boneHU); // Adjust threshold as per your image
            //skinThresholdFilter.ReplaceInOn();
            //skinThresholdFilter.SetInValue(255);
            //skinThresholdFilter.SetOutValue(0);
            //skinThresholdFilter.Update();

            //// Step 3: Contour extraction
            //vtkMarchingContourFilter contourFilter = vtkMarchingContourFilter.New();
            //contourFilter.SetInputData(skinThresholdFilter.GetOutput());
            ////contourFilter.SetValue(0, 255); // Contour value
            //contourFilter.Update();

            //// Visualization
            //vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            //mapper.SetInputData(contourFilter.GetOutput());

            //vtkActor actor = vtkActor.New();
            //actor.SetMapper(mapper);

            //-----------Drr Option----------

            vtkImageThreshold bonethresholdFilter = vtkImageThreshold.New();
            bonethresholdFilter.SetInputData(imageData);
            bonethresholdFilter.ThresholdByUpper(boneHU); // Set threshold value
            bonethresholdFilter.SetInValue(200);
            bonethresholdFilter.SetOutValue(100);
            bonethresholdFilter.Update();
            vtkImageData segmentedImageData = bonethresholdFilter.GetOutput();

            vtkImageThreshold fatThresholdFilter = vtkImageThreshold.New();
            fatThresholdFilter.SetInputData(segmentedImageData);
            fatThresholdFilter.ThresholdByUpper(fatHU); // Set threshold value
            fatThresholdFilter.SetInValue(31);
            fatThresholdFilter.SetOutValue(0);
            fatThresholdFilter.Update();
            

            vtkImageReslice reslice = vtkImageReslice.New();
            //reslice.SetInputData(segmented);
            reslice.SetOutputDimensionality(2);
            reslice.SlabTrapezoidIntegrationOn();
            reslice.SetSlabSliceSpacingFraction(2);
            reslice.InterpolateOn();
            reslice.SetSlabModeToMax();
            reslice.SetSlabNumberOfSlices(1000);
            //Axis Rotation
            vtkTransform transformAxis = new();
            transformAxis.RotateX(-90);
            //transformAxis.RotateY(90);
            reslice.SetResliceAxes(transformAxis.GetMatrix());
            reslice.Update();
            /*vtkImageGaussianSmooth gaussianSmoothFilter = vtkImageGaussianSmooth.New();
            gaussianSmoothFilter.SetInputData(segmentedImageData);
            //gaussianSmoothFilter.SetStandardDeviations(1.75, 1.75, 0);
            gaussianSmoothFilter.SetRadiusFactor(1);
            gaussianSmoothFilter.Update();
            vtkImageData filteredImageData = gaussianSmoothFilter.GetOutput();*/

            vtkImageActor actor = new();
            actor.SetInputData(reslice.GetOutput());
            //-------------------------------------------------

            var renderWindow = renderWindowControl.RenderWindow;
            
            //vtkRenderWindowInteractor renderWindowInteractor = vtkRenderWindowInteractor.New();
            //renderWindowInteractor.SetRenderWindow(renderWindow);
            
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(actor);
            //renderWindow.GetRenderers().GetFirstRenderer().SetBackground(1,1,1);
            renderWindow.GetRenderers().GetFirstRenderer().ResetCamera();

            //renderWindowInteractor.Start();
        }

        private void filterAndDrr()
        {
            
            vtkDICOMImageReader reader = vtkDICOMImageReader.New();
            reader.SetDirectoryName(dicomDirectory);
            reader.Update();

            var origin = reader.GetImagePositionPatient();

            vtkImageData imageData = reader.GetOutput();

            var thresholdFilter = vtkImageThreshold.New();
            thresholdFilter.SetInputData(imageData);
            // Set thresholds for the specific tissue - this example is for bone
            //thresholdFilter.ThresholdByUpper(boneHU); // Bone threshold
            //thresholdFilter.ThresholdBetween(softTissueHU, boneHU);
            thresholdFilter.ReplaceInOn();
            thresholdFilter.SetInValue(255); // Set inside value to 1
            thresholdFilter.ReplaceOutOn();
            thresholdFilter.SetOutValue(0); // Set outside value to 0
            thresholdFilter.Update();

            
            

            vtkImageReslice reslice = vtkImageReslice.New();
            reslice.SetInputData(thresholdFilter.GetOutput());
            reslice.SetOutputDimensionality(2);
            reslice.SlabTrapezoidIntegrationOn();
            reslice.SetSlabSliceSpacingFraction(2);
            reslice.InterpolateOn();
            reslice.SetSlabModeToMax();
            reslice.SetSlabNumberOfSlices(1000);
            //Axis Rotation
            vtkTransform transformAxis = new();
            transformAxis.RotateX(-90);
            reslice.SetResliceAxes(transformAxis.GetMatrix());

            reslice.Update();
            vtkImageActor actor = vtkImageActor.New();
            actor.SetInputData(reslice.GetOutput());
            
            //actor.GetProperty().SetOpacity(opacityTransferFunction);

            var renderWindow = renderWindowControl.RenderWindow;

            vtkRenderWindowInteractor renderWindowInteractor = vtkRenderWindowInteractor.New();
            renderWindowInteractor.SetRenderWindow(renderWindow);

            //renderWindow.GetRenderers().GetFirstRenderer().AddVolume(volume);
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(actor);
            //renderWindow.GetRenderers().GetFirstRenderer().AddActor(imageActor);
            //renderWindow.GetRenderers().GetFirstRenderer().SetBackground(0.0, 0.0, 0.0);

            // Adjust camera, lighting, etc., to mimic an X-ray
            // [...]
            //renderWindowInteractor.Start();
            //renderWindow.Render();
        }
        public vtkActor FilterAndCreateActor(vtkImageData inputImage)
        {
            // Create vtkImageShiftScale to scale the input image data to [0, 255]
            vtkImageShiftScale shiftScale = vtkImageShiftScale.New();
            shiftScale.SetInputData(inputImage);
            shiftScale.SetOutputScalarTypeToUnsignedChar();
            shiftScale.SetShift(-inputImage.GetScalarRange()[0]);
            shiftScale.SetScale(255 / (inputImage.GetScalarRange()[1] - inputImage.GetScalarRange()[0]));
            shiftScale.Update();

            // Create vtkImageThreshold to segment different tissues
            vtkImageThreshold threshold = vtkImageThreshold.New();
            //threshold.SetInputData(shiftScale.GetOutput());
            threshold.SetInputData(inputImage);
            threshold.SetOutValue(0);
            //threshold.ReplaceInOn();
            //threshold.ReplaceOutOn();

            // Define thresholds for different tissues
            //threshold.SetInValue(1); // Air
            //threshold.ThresholdBetween(-1000, -500);
            //threshold.Update();

            threshold.SetInValue(255); // Bone
            threshold.ThresholdBetween(200, 300);
            threshold.Update();

            //threshold.SetInValue(3); // Metal
            //threshold.ThresholdBetween(600, 700);
            //threshold.Update();

            threshold.SetInValue(50); // Soft tissue
            threshold.ThresholdBetween(30, 80);
            threshold.Update();

            //threshold.SetInValue(5); // Fat
            //threshold.ThresholdBetween(-50, -20);
            //threshold.Update();

            // Create vtkContourFilter to extract contours
            //vtkContourFilter contourFilter = vtkContourFilter.New();
            //contourFilter.SetInputConnection(threshold.GetOutputPort());
            //contourFilter.SetValue(0, 50); // Contour at threshold 1
            //contourFilter.Update();

            // Create mapper and actor
            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(threshold.GetOutputPort());


            vtkActor actor = vtkActor.New();
            actor.SetMapper(mapper);

            return actor;
        }
        private void filterDrrWithThrashould(vtkImageData imageData)
        {
            double fatHU = -274;
            double airHU = -999;
            double softTissueHU = -24;
            double boneHU = 176;
            double metalHU = 776;

            vtkImageThreshold thresholdBone = vtkImageThreshold.New();
            thresholdBone.SetInputData(imageData);
            //threshold.ReplaceInOn();
            //threshold.ReplaceOutOn();
            //threshold.SetInValue(255);
            thresholdBone.ThresholdByUpper(boneHU);// Bone
            //threshold.SetOutputScalarTypeToUnsignedInt();
            thresholdBone.Update();
            //vtkImageThreshold thresholdTissue = new();
            ////threshold.SetInValue(255); // Soft tissue
            //thresholdTissue.ThresholdBetween(airHU, boneHU);
            //thresholdTissue.Update();

            //vtkImageMathematics blend = new vtkImageMathematics();
            ////blend.SetOperationToAdd();
            //blend.SetInput1Data(thresholdBone.GetOutput());
            //blend.SetInput2Data(thresholdTissue.GetOutput());
            //blend.Update();

            double[] scalarRange = thresholdBone.GetOutput().GetPointData().GetScalars().GetRange();
            var colorWindow = scalarRange[1] - scalarRange[0];
            var colorLevel = colorWindow / 2;
            vtkImageMapToWindowLevelColors windowLevelColors = new();
            windowLevelColors.SetInputData(thresholdBone.GetOutput());
            windowLevelColors.SetWindow(500);
            windowLevelColors.SetLevel(50);
            windowLevelColors.Update();
            //visualizeDrr(threshold.GetOutput());
            visualizeDrr(windowLevelColors.GetOutput());
            //visualizeDrr(blend.GetOutput());
            
        }
        
        private void filterDrrWithLookupTable(vtkImageData imageData)
        {
            // Create a lookup table
            vtkLookupTable lookupTable = new vtkLookupTable();
            //lookupTable.SetNumberOfTableValues(256); // Set the number of colors
            lookupTable.Build(); // Build the lookup table

            // Set color and opacity mapping
            //for (int i = 0; i < 256; i++)
            //{
            //    double alpha = i / 255.0; // Opacity (from 0 to 1)
            //    double color = i / 255.0; // Grayscale color (from 0 to 1)
            //    lookupTable.SetTableValue(i, color, color, color, alpha);
            //}

            lookupTable.SetTableValue(1, .5, .5, .5, 1);
            // Apply the lookup table to the image data
            vtkImageMapToColors mapColors = new vtkImageMapToColors();
            mapColors.SetLookupTable(lookupTable);
            mapColors.SetInputData(imageData);
            mapColors.Update();

            // Get the output as image data
            vtkImageData outputImageData = mapColors.GetOutput();
            visualizeDrr(outputImageData);

        }
        private void visualizeDrr(vtkImageData drrimage)
        {
            var renderWindow = renderWindowControl.RenderWindow;

            //-----------Display the DRR on a render window---------

            vtkImageActor actor = new();
            actor.SetInputData(drrimage);
            //renderWindow.GetRenderers().GetFirstRenderer().AddActor(FilterAndCreateActor(drrimage));
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(actor);


            //----------Show Axis---------------
            vtkAxesActor axeis = new();
            axeis.SetTotalLength(300, 300, 300);
            //axeis.SetUserTransform(transformAxis);
            //axeis.SetUserMatrix(combineMatrix);
            //renderWindow.GetRenderers().GetFirstRenderer().AddActor(axeis);
            //-------------------------------

            //------------Volume-------------
            //vtkFixedPointVolumeRayCastMapper volumeMapper = vtkFixedPointVolumeRayCastMapper.New();
            //volumeMapper.SetInputData(rotatedImageData);
            //vtkVolume v = new();
            //v.SetMapper(volumeMapper);
            //renderWindow.GetRenderers().GetFirstRenderer().AddVolume(v);
            //--------------------------

            //renderWindow.GetRenderers().GetFirstRenderer().SetBackground(1, 1, 1);
            renderWindow.GetRenderers().GetFirstRenderer().ResetCamera();

            renderWindow.Render();
        }
        private vtkImageData transformeImageData(vtkImageData imageData)
        {
            var spacing = imageData.GetSpacing();
            var dimension = imageData.GetDimensions();
            var origin = imageData.GetOrigin();
            var extend = imageData.GetExtent();

            //Rotate ImageData
            vtkTransform transform = new();
            transform.RotateX(90);

            vtkImageData transformedImageData = vtkImageData.New();
            transformedImageData.SetDimensions(dimension[0], dimension[2], dimension[1]);
            transformedImageData.SetExtent(extend[0], extend[1], extend[4], extend[5], extend[2], extend[3]);
            transformedImageData.SetOrigin(origin[0], origin[2], origin[1]);
            transformedImageData.SetSpacing(spacing[0], spacing[2], spacing[1]);


            vtkTransformFilter transformFilter = vtkTransformFilter.New();
            transformFilter.SetInputData(imageData);
            transformFilter.SetTransform(transform);
            transformFilter.Update();
            transformedImageData.GetPointData().SetScalars(transformFilter.GetOutput().GetPointData().GetScalars());

            return transformedImageData;

        }
        private void marchingcubeTest(vtkImageData imageData)
        {
            
            //vtkNamedColors colors = new();
            //colors.SetColor();

            vtkMarchingCubes skin = new();
            skin.SetInputData(imageData);
            skin.SetValue(0, 500);

            vtkStripper skinStripper = new();
            skinStripper.SetInputData(skin.GetOutput());

            vtkPolyDataMapper skinmapper = new();
            skinmapper.SetInputData(skinStripper.GetOutput());
            skinmapper.ScalarVisibilityOff();

            vtkActor skinactor = new();
            skinactor.SetMapper(skinmapper);
            //skinactor.GetProperty().SetDiffuseColor();

            var renderWindow = renderWindowControl.RenderWindow;
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(skinactor);
        }

       
    }
    
    
}