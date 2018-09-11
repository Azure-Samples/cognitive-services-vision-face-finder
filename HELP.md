# Help for Face Finder

Face Finder provides two modes of operation.

The first mode searches a folder for images that contain at least one face and displays information about these images and faces. Basic searches can be filtered by age range and gender.

To start:

1. Click the expander button on the top right of the screen.
1. Enter your Computer Vision and Face keys in the appropriate text boxes.
1. Click **Select Folder** and select a folder containing images that you want to search for faces.
1. Click **Find Faces**. If you want to stop the search early, click  **Cancel**.

Note: If **Get thumbnail** is selected, a subfolder named *FaceThumbnails* is created in the selected folder to store the thumbnails created by the Computer Vision service.

The following screenshot shows the opening screen with the setting pane opened.

![Opening screenshot](Images/facefinder-opening-screen.png)

The following screenshot shows a search for males between the ages of 50 and 70.

![Screenshot after filtered search](Images/facefinder-after-search.png)

The second mode additionally filters the images to those with a face that match a specified person.

To filter by person:

1. Type a name for the person in the combo box and click **Add Person**.
1. Select one or more images of this person, using the Ctrl and Shift keys for multiple selections. Each selected image should contain only one face and the face should be a view showing both eyes.
1. When selection is complete, click **Add Faces**.
1. Click **Display** to see the selected images in the left pane.
1. Select the **Match person** checkbox.
1. Click **Find Faces** to start the search over again.

The following screenshot shows the results after searching for images that match the specified person.

![Screenshot after filtered search](Images/facefinder-person-match.png)

A person and their associated images are persisted. To delete a person:

1. Select the person in the combo box.
1. Click **Delete**. A confirmation dialog appears for approval.

Images in these screenshots are from [Labeled Faces in the Wild](http://vis-www.cs.umass.edu/lfw/).