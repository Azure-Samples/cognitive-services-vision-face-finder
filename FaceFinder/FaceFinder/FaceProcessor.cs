using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceFinder
{
    /// <summary>
    /// Creates a PersonGroup containing people having one or more associated faces.
    /// Processes faces in images to find matches for a specified person.
    /// Dependencies: Face service.
    /// </summary>
    class FaceProcessor : ViewModelBase
    {
        private readonly IFaceClient faceClient;

        private const string PERSONGROUPID = "ff-person-group-id";
        private readonly Person emptyPerson = new Person(Guid.Empty, string.Empty);

        // Set in GetOrCreatePersonAsync()
        private Person searchedForPerson;

        // A trained PersonGroup has at least 1 added face for the specifed person
        // and has successfully completed the training process at least once.
        private bool isPersonGroupTrained;
        public bool IsPersonGroupTrained
        {
            get => isPersonGroupTrained;
            set => SetProperty(ref isPersonGroupTrained, value);
        }

        public FaceProcessor(IFaceClient faceClient)
        {
            this.faceClient = faceClient;
            searchedForPerson = emptyPerson;
        }

        /// <summary>
        /// Returns all faces detected in an image stream
        /// </summary>
        /// <param name="stream">An image</param>
        /// <returns>A list of detected faces or an empty list</returns>
        public async Task<IList<DetectedFace>> GetFaceListAsync(FileStream stream)
        {
            try
            {
                return await faceClient.Face.DetectWithStreamAsync(stream, true, false,
                    new FaceAttributeType[]
                        { FaceAttributeType.Age, FaceAttributeType.Gender });
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetFaceListAsync: " + e.Message);
                MessageBox.Show(e.Message, "GetFaceListAsync");
            }
            return Array.Empty<DetectedFace>();
        }

        /// <summary>
        /// Returns all PersonGroup's associated with the Face subscription key
        /// </summary>
        /// <returns>A list of PersonGroup's or an empty list</returns>
        public async Task<IList<PersonGroup>> GetAllPersonGroupsAsync()
        {
            try
            {
                return await faceClient.PersonGroup.ListAsync();
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetAllPersonGroupsAsync: " + e.Message);
                MessageBox.Show(e.Message, "GetAllPersonGroupsAsync");
            }
            return new List<PersonGroup>();
        }

        /// <summary>
        /// Returns all Person.Name's associated with PERSONGROUPID
        /// </summary>
        /// <returns>A list of Person.Name's or an empty list</returns>
        public async Task<IList<string>> GetAllPersonNamesAsync()
        {
            IList<string> names = new List<string>();
            try
            {
                IList<Person> personNames = await faceClient.PersonGroupPerson.ListAsync(PERSONGROUPID);
                foreach(Person person in personNames)
                {
                    // Remove appended "-group".
                    names.Add(person.Name.Replace("_", " "));
                }
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetAllPersonNamesAsync: " + e.Message);
            }
            return names;
        }

        /// <summary>
        /// Gets or creates a PersonGroup with PERSONGROUPID
        /// </summary>
        public async Task GetOrCreatePersonGroupAsync()
        {
            try
            {
                PersonGroup personGroup= null;

                // Get PersonGroup if it exists.
                IList<PersonGroup> groups = await faceClient.PersonGroup.ListAsync();
                foreach (PersonGroup group in groups)
                {
                    if (group.PersonGroupId == PERSONGROUPID)
                    {
                        personGroup = group;
                        break;
                    }
                }

                if (personGroup == null)
                {
                    // PersonGroup doesn't exist, create it.
                    await faceClient.PersonGroup.CreateAsync(PERSONGROUPID);
                    personGroup = (await faceClient.PersonGroup.ListAsync())[0];
                }
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + personGroup.PersonGroupId);
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
            }
        }

        /// <summary>
        /// Gets or creates a PersonGroupPerson
        /// </summary>
        /// <param name="name">PersonGroupPerson.Name</param>
        /// <param name="GroupInfos">A collection specifying the file paths of images associated with <paramref name="name"/></param>
        public async Task GetOrCreatePersonAsync(string name, ObservableCollection<ImageInfo> GroupInfos)
        {
            if (string.IsNullOrWhiteSpace(name)) { return; }
            Debug.WriteLine("GetOrCreatePersonAsync: " + name);

            GroupInfos.Clear();
            IsPersonGroupTrained = false;

            searchedForPerson = emptyPerson;
            string personName = ConfigurePersonName(name);

            try
            {
                IList<Person> people =
                    await faceClient.PersonGroupPerson.ListAsync(PERSONGROUPID);

                // Get Person if it exists.
                foreach(Person person in people)
                {
                    if (person.Name.Equals(personName))
                    {
                        searchedForPerson = person;
                        if(searchedForPerson.PersistedFaceIds.Count > 0)
                        {
                            await DisplayFacesAsync(GroupInfos);
                            IsPersonGroupTrained = true;
                        }
                        return;
                    }
                }

               // Person doesn't exist, create it.
                await faceClient.PersonGroupPerson.CreateAsync(PERSONGROUPID, personName);

                // MUST re-query to get completely formed PersonGroupPerson
                searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(PERSONGROUPID))[0];
                return;
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetOrCreatePersonAsync: " + ae.Message);
                searchedForPerson = emptyPerson;
            }
        }

        // Each image should contain only 1 detected face; otherwise, must specify face rectangle.
        /// <summary>
        /// Adds PersistedFace's to 'personName'
        /// </summary>
        /// <param name="selectedItems">A collection specifying the file paths of images to be associated with searchedForPerson</param>
        /// <param name="GroupInfos"></param>
        public async Task AddFacesToPersonAsync(
            IList<ImageInfo> selectedItems, ObservableCollection<ImageInfo> GroupInfos)
        {
            if ((searchedForPerson == null) || (searchedForPerson == emptyPerson))
            {
                Debug.WriteLine("AddFacesToPersonAsync, no searchedForPerson");
                return;
            }

            IList<string> faceImagePaths = await GetFaceImagePathsAsync();

            foreach (ImageInfo info in selectedItems)
            {
                string imagePath = info.FilePath;

                // Check for duplicate images
                if (faceImagePaths.Contains(imagePath)) { continue; } // Face already added to Person

                using (FileStream stream = new FileStream(info.FilePath, FileMode.Open))
                {
                    PersistedFace persistedFace =
                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(
                            PERSONGROUPID, searchedForPerson.PersonId, stream, imagePath);
                }

                GroupInfos.Add(info);
            }

            // MUST re-query to get updated PersonGroupPerson
            searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(PERSONGROUPID))[0];

            if(searchedForPerson.PersistedFaceIds.Count == 0)
            {
                IsPersonGroupTrained = false;
                return;
            }

            await faceClient.PersonGroup.TrainAsync(PERSONGROUPID);

            IsPersonGroupTrained = await GetTrainingStatusAsync();
        }

        /// <summary>
        /// Determines whether a given face matches searchedForPerson 
        /// </summary>
        /// <param name="faceId">PersistedFace.PersistedFaceId</param>
        /// <param name="newImage">On success, contains confidence value</param>
        /// <returns>Whether <paramref name="faceId"/> matches searchedForPerson</returns>
        public async Task<bool> MatchFaceAsync(Guid faceId, ImageInfo newImage)
        {
            if((faceId == Guid.Empty) || (searchedForPerson?.PersonId == null)) { return false; }

            VerifyResult results;
            try
            {
                results = await faceClient.Face.VerifyFaceToPersonAsync(
                    faceId, searchedForPerson.PersonId, PERSONGROUPID);
                newImage.Confidence = results.Confidence.ToString("P");

            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("MatchFaceAsync: " + ae.Message);
                return false;
            }

            // TODO: add Confidence slider
            // Default: True if similarity confidence is greater than or equal to 0.5.
            // Can change by specifying VerifyResult.Confidence.
            return results.IsIdentical;
        }

        /// <summary>
        /// Sets 'GroupInfos', which specifies the file paths of images associated with searchedForPerson
        /// </summary>
        /// <param name="GroupInfos">On success, contains image info associated with searchedForPerson</param>
        public async Task DisplayFacesAsync(ObservableCollection<ImageInfo> GroupInfos)
        {
            IList<string> faceImagePaths = await GetFaceImagePathsAsync();
            if(faceImagePaths == Array.Empty<string>()) { return; }

            foreach (string path in faceImagePaths)
            {
                ImageInfo groupInfo = new ImageInfo();
                groupInfo.FilePath = path;
                GroupInfos.Add(groupInfo);
            }
        }

        /// <summary>
        /// Deletes searchedForPerson
        /// </summary>
        /// <param name="GroupInfos"></param>
        /// <param name="GroupNames"></param>
        /// <param name="askFirst">true to display a confirmation dialog</param>
        public async Task DeletePersonAsync(ObservableCollection<ImageInfo> GroupInfos,
            ObservableCollection<string> GroupNames, bool askFirst = true)
        {
            MessageBoxResult result;
            try
            {
                result = askFirst ?
                    MessageBox.Show("Delete " + searchedForPerson + " and its training images?",
                        "Delete " + searchedForPerson, MessageBoxButton.OKCancel, MessageBoxImage.Warning) :
                    MessageBoxResult.OK;

                if (result == MessageBoxResult.OK)
                {
                    GroupInfos.Clear();
                    await faceClient.PersonGroupPerson.DeleteAsync(PERSONGROUPID, searchedForPerson.PersonId);
                    string personName = searchedForPerson.Name.Replace("_", " ");
                    if (GroupNames.Contains(personName))
                    {
                        GroupNames.Remove(personName);
                        Debug.WriteLine("DeletePersonAsync: " + personName);
                    }
                    searchedForPerson = emptyPerson;
                }
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("DeletePersonAsync: " + ae.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine("DeletePersonAsync: " + e.Message);
            }
        }

        // TODO: add progress indicator
        private async Task<bool> GetTrainingStatusAsync()
        {
            TrainingStatus trainingStatus = null;
            try
            {
                do
                {
                    trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(PERSONGROUPID);
                    await Task.Delay(1000);
                } while (trainingStatus.Status == TrainingStatusType.Running);
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetTrainingStatusAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "GetTrainingStatusAsync");
                return false;
            }
            return trainingStatus.Status == TrainingStatusType.Succeeded;
        }

        // PersistedFace.UserData stores the associated image file path.
        // Returns the image file paths associated with each PersistedFace
        private async Task<IList<string>> GetFaceImagePathsAsync()
        {
            IList<string> faceImagePaths = new List<string>();

            IList<Guid> persistedFaceIds = searchedForPerson.PersistedFaceIds;
            foreach(Guid pfid in persistedFaceIds)
            {
                PersistedFace face = await faceClient.PersonGroupPerson.GetFaceAsync(
                    PERSONGROUPID, searchedForPerson.PersonId, pfid);
                if (!string.IsNullOrEmpty(face.UserData))
                {
                    string imagePath = face.UserData;
                    if (File.Exists(imagePath))
                    {
                        faceImagePaths.Add(imagePath);
                        Debug.WriteLine("GetFaceImagePathsAsync: " + imagePath);
                    }
                    else
                    {
                        await faceClient.PersonGroupPerson.DeleteFaceAsync(
                            PERSONGROUPID, searchedForPerson.PersonId, pfid);
                        Debug.WriteLine("GetFaceImagePathsAsync, file not found, deleting reference: " + imagePath);
                    }
                }
            }
            return faceImagePaths;
        }

        private string ConfigurePersonName(string name)
        {
            return name.Replace(" ", "_");
        }
    }
}
