# Program notes for Face Finder

Computer Vision calls:

* DescribeImageInStreamAsync
* GenerateThumbnailInStreamAsync
* RecognizePrintedTextInStreamAsync

Face calls:

* Face.DetectWithStreamAsync
* Face.VerifyFaceToPersonAsync
* PersonGroup.CreateAsync
* PersonGroup.DeleteAsync
* PersonGroup.GetAsync
* PersonGroup.ListAsync
* PersonGroup.TrainAsync
* PersonGroup.GetTrainingStatusAsync
* PersonGroupPerson.CreateAsync
* PersonGroupPerson.AddFaceFromStreamAsync
* PersonGroupPerson.GetFaceAsync
* PersonGroupPerson.ListAsync

Subscription keys are persisted in plain text in [IsolatedStorage](https://docs.microsoft.com/dotnet/standard/io/isolated-storage?view=netframework-4.7.2). On Windows 10 and Windows 7, the location is *<SYSTEMDRIVE>\Users\<user>\AppData\Local*. IsolatedStorage keeps the keys out of program code but is not secure.

Image files are processed and displayed by image type (bitmap, gif, jpg, png) in the order specified in the `GetImageFiles` method.

Image metadata: jpg's supply date taken and title; png's just date taken.

Each training image should contain only one detected face, otherwise the rectangle delineating the face must be specified.

Individual training faces cannot be deleted without deleting the containing `PersonGroup`. This feature will be implemented in the future.

If you add images to the currently selected folder, you must reselect the folder for the new images to get processed.

The default confidence level for a matching face is `VerifyResult.Confidence` = 0.5.

Performance is related to image size due to uploading and processing time. Small images on the order of 20 KB can provide good results.

MUST requery face entities to get updated values.

### Future features:

* Search subfolders
* Confidence slider for what constitutes a matched face
* Number picker to limit number of images processed
* Azure blob storage
* Persistence of settings other than subscription keys and endpoints
