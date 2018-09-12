---
services: cognitive-services, computer vision, face
platforms: dotnet, c#
author: easyj2j
---

# Face Finder

This sample searches a folder for image files containing a face. Selected attributes of the image and face are displayed. Searches can be filtered by age range, gender, and whether the face matches a specified person. The sample uses the [Windows client libraries](https://www.nuget.org/packages?q=Microsoft.Azure.CognitiveServices.Vision) for the [Computer Vision](https://docs.microsoft.com/azure/cognitive-services/computer-vision/) and [Face](https://docs.microsoft.com/azure/cognitive-services/face/) services of [Microsoft Cognitive Services](https://docs.microsoft.com/azure/cognitive-services/).

## Features

* Parses a folder for image files of type bmp, gif, jpg, and png.
* Processes the image files to detect images containing a face.
* Displays thumbnails of each image that has a face, along with attributes associated with the image and face.
* Available attributes are:
  * age and gender
  * caption (as a tool tip)
  * printed character recognition (OCR)
  * date image taken and title, if available
* Image files can be filtered on:
  * age range
  * gender
  * matching person/face
* Finds all images with a face that matches a specified person.

## Getting Started

### Prerequisites

* You need **subscription keys** for the Computer Vision and Face services to run the sample. You can get free trial subscription keys from [Try Cognitive Services](https://azure.microsoft.com/try/cognitive-services/).
* Any edition of [Visual Studio 2017](https://www.visualstudio.com/downloads/) with the .NET Desktop application development workload installed.

### Quickstart

1. Clone or download the repository.
1. Open the *FaceFinder* folder in the repository.
1. Double-click the *FaceFinder.sln* file, which opens the project in Visual Studio.
1. Build the project, which installs the Computer Vision and Face service NuGet packages.
1. Run the program.

## Walkthrough

Face Finder provides two modes of operation.

The first mode searches a folder for images that contain at least one face and displays information about these images and faces. Searches can be filtered by age range and gender.

To start:

1. Click the expander button on the upper right of the screen.
1. Enter your Computer Vision and Face **subscription keys** in the appropriate text boxes. The keys and endpoints are stored in **IsolatedStorage**.
1. Click **Select Folder** and browse to a folder containing images that you want to search for faces.
1. Click **Find Faces**, which searches the folder for image files and then analyzes these files for faces. If you want to stop the search early, click  **Cancel**.

Note: When **Get thumbnail** is selected, a subfolder named *FaceThumbnails* is created under the selected folder to store the thumbnails created by the Computer Vision service.

The following screenshot shows the opening screen with the settings pane opened.

![Opening screenshot](Images/facefinder-opening-screen.png)

The following screenshot shows a search for males between the ages of 50 and 70. Note the tool tip on the third image showing the caption from Computer Vision's analysis of the image.

![Screenshot after filtered search](Images/facefinder-after-search.png)

The second mode additionally filters the images to those with a face that match a specified person.

To filter by person:

1. Type a name for the person in the combo box and click **Add Person**.
1. Select one or more images of this person, using the Ctrl and Shift keys for multiple selections. Each selected image should contain only one face and the face should be a view showing both eyes.
1. When selection is complete, click **Add Faces**, which associates the faces with the specified person.
1. Click **Display** to see the selected images in the left pane.
1. Select the **Match person** checkbox.
1. Click **Find Faces** to start the search over again. This time, only images that match the person are displayed.

The following screenshot shows the results after searching for images that match the specified person.

![Screenshot after filtered search](Images/facefinder-person-match.png)

A person and their associated images are persisted. To delete a person and their images:

1. Select the person in the combo box.
1. Click **Delete**. A confirmation dialog appears for approval.

Images in these screenshots are from [Labeled Faces in the Wild](http://vis-www.cs.umass.edu/lfw/).

## Resources

* [Computer Vision service documentation](https://docs.microsoft.com/azure/cognitive-services/computer-vision/)
* [Computer Vision API - v2.0](https://westus.dev.cognitive.microsoft.com/docs/services/5adf991815e1060e6355ad44/operations/56f91f2e778daf14a499e1fa)
* [Microsoft.Azure.CognitiveServices.Vision.ComputerVision 3.2.0](https://www.nuget.org/packages/Microsoft.Azure.CognitiveServices.Vision.ComputerVision/3.2.0) client library NuGet package
* [Face service documentation](https://docs.microsoft.com/azure/cognitive-services/face/)
* [Face API](https://docs.microsoft.com/azure/cognitive-services/face/apireference)
* [Microsoft.Azure.CognitiveServices.Vision.Face 2.2.0-preview](https://www.nuget.org/packages/Microsoft.Azure.CognitiveServices.Vision.Face/2.2.0-preview) client library NuGet package