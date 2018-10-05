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

## Service notes

Each training image should contain only one detected face; otherwise, the rectangle delineating the face to match would have to be specified.

The default confidence level for a matching face is `VerifyResult.Confidence` = 0.5. This value can be changed in the `FaceProcessor.MatchFaceAsync` method.

Face entities (`PersonGroup, Person, PersistedFace`) aren't fully formed after a method call and MUST be requeried to get all property values. For example:

```c#
Person person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, personGroupName, personUserData);

person.PersonId: someGuid
person.Name: null
person.UserData: null

Person samePerson = await faceClient.PersonGroupPerson.GetAsync(personGroupId, person.PersonId);

samePerson.PersonId: someGuid
samePerson.Name: personGroupName
samePerson.UserData: personUserData
```

## Misc

Subscription keys and endpoints are persisted in plain text in [IsolatedStorage](https://docs.microsoft.com/dotnet/standard/io/isolated-storage?view=netframework-4.7.2). On Windows 10 and Windows 7, the location is *<SYSTEMDRIVE>\Users\<user>\AppData\Local*. IsolatedStorage keeps the keys out of program code but isn't secure.

Image files are processed and displayed by image type (bitmap, gif, jpg, png) in the order specified in the `GetImageFiles` method.

Image metadata: jpg's supply date taken and title; png's just date taken.

Individual training faces cannot be deleted without deleting the containing `PersonGroup`. This feature will be implemented in the future.

If you add or delete images in the currently selected folder, you must reselect the folder for the program to recognize the changes. If you search for faces after deleting an image from the current folder, an error dialog is displayed and processing stops. Reselect the folder and then perform another search.

Performance is related to image size due to uploading and processing time. Small images on the order of 20 KB can provide good results.

### Future features

* Confidence slider for what constitutes a matched face
* Number picker to limit number of images processed
* Azure blob storage
* Search subfolders
* Persistence of settings other than subscription keys and endpoints
