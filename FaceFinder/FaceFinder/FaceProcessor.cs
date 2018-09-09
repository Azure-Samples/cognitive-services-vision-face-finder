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
    /// Creates a PersonGroup of a single person to search for in images.
    /// Processes faces in images to find this person.
    /// Dependencies: Face service.
    /// </summary>
    class FaceProcessor : ViewModelBase
    {
        private IFaceClient faceClient;

        private string personGroupId = string.Empty;
        private string personGroupName = "PersonGroup";
        private readonly Person defaultEmptyPerson = new Person(Guid.Empty, string.Empty);
        private Person searchedForPerson;

        // A trained PersonGroup has at least 1 added face and has 
        // successfully completed the training process at least once.
        public bool IsPersonGroupTrained { get; private set; }

        public string PersonGroupName
        {
            get => personGroupName;
            set => SetProperty(ref personGroupName, value);
        }

        public FaceProcessor()
        {
            faceClient = ((App)Application.Current).faceClient;
            searchedForPerson = defaultEmptyPerson;
        }

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

        public async Task<IList<string>> GetAllPersonGroupNamesAsync()
        {
            IList<string> personGroupNames = new List<string>();
            try
            {
                IList<PersonGroup> personGroups = await faceClient.PersonGroup.ListAsync();
                foreach(PersonGroup group in personGroups)
                {
                    // Remove appended "-group".
                    personGroupNames.Add(group.Name.Substring(0, group.Name.Length - 6).ToUpper());
                }
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetAllPersonGroupsNamesAsync: " + e.Message);
                MessageBox.Show(e.Message, "GetAllPersonGroupsAsync");
            }
            return personGroupNames;
        }

        // TODO: split into 2 methods
        public async Task GetOrCreatePersonGroupAsync(string name,
            ObservableCollection<ImageInfo> GroupInfos)
        {
            Debug.WriteLine("GetOrCreatePersonGroupAsync: " + name);
            if (string.IsNullOrWhiteSpace(name)) { return; }

            GroupInfos.Clear();

            searchedForPerson = defaultEmptyPerson;

            string personName = ConfigurePersonName(name);

            PersonGroupName = personName + "-group";

            // lowercase char, digit, '-', or '_'; maximum length 64
            personGroupId = PersonGroupName + "-id";

            // Get existing PersonGroup.
            PersonGroup group;
            try
            {
                group = await faceClient.PersonGroup.GetAsync(personGroupId);
                IList<Person> people = await faceClient.PersonGroupPerson.ListAsync(personGroupId);
                if(people.Count == 0)
                {
                    // Invalid PersonGroup
                    Debug.WriteLine("GetOrCreatePersonGroupAsync: " +
                        "No PersonGroupPerson associated with PersonGroup; deleting PersonGroup");
                    MessageBox.Show("Invalid PersonGroup, deleting", "GetOrCreatePersonGroupAsync");
                    await DeletePersonGroupAsync(name, false);
                    return;
                }

                searchedForPerson = people[0];

                if(searchedForPerson.PersistedFaceIds.Count > 0)
                {
                    IsPersonGroupTrained = true;
                    await DisplayFacesInGroupAsync(searchedForPerson, GroupInfos);
                }
                return;
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
                if(ae.Response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    MessageBox.Show(ae.Message, "GetOrCreatePersonGroupAsync");
                    searchedForPerson = defaultEmptyPerson;
                    personGroupId = string.Empty;
                    return;
                }
            }

            // No existing PersonGroup; create one.
            try
            {
                await faceClient.PersonGroup.CreateAsync(personGroupId, PersonGroupName);
            }
            catch (APIErrorException ae)
            {
                personGroupId = string.Empty;
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "GetOrCreatePersonGroupAsync");
                return;
            }

            // PersonGroup successfully created. Create PersonGroupPerson (1 per PersonGroup)
            try
            {
                //GroupInfos.Clear();
                searchedForPerson = await faceClient.PersonGroupPerson.CreateAsync(
                    personGroupId, personName);

                // MUST re-query to get completely formed PersonGroupPerson
                searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(personGroupId))[0];
                return;
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "GetOrCreatePersonGroupAsync");
                searchedForPerson = defaultEmptyPerson;
                await faceClient.PersonGroup.DeleteAsync(personGroupId);
                personGroupId = string.Empty;
                return;
            }
        }

        // Each image must contain only 1 detected face.
        public async Task AddFacesToPersonAsync(string personName,
            IList<ImageInfo> selectedItems, ObservableCollection<ImageInfo> GroupInfos)
        {
            if ((searchedForPerson == null) || (searchedForPerson == defaultEmptyPerson))
            {
                Debug.WriteLine("AddFacesToPersonAsync, no searchedForPerson, personName = " + personName);
                return;
            }

            IList<string> faceImagePaths = await GetFaceImagePathsAsync(searchedForPerson);

            foreach (ImageInfo info in selectedItems)
            {
                string imagePath = info.FilePath;

                // Check for duplicate images
                if (faceImagePaths.Contains(imagePath)) { continue; } // Face already added to Person

                using (FileStream stream = new FileStream(info.FilePath, FileMode.Open))
                {
                    PersistedFace persistedFace =
                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(
                            personGroupId, searchedForPerson.PersonId, stream, imagePath);
                }

                GroupInfos.Add(info);
            }

            searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(personGroupId))[0];

            if(searchedForPerson.PersistedFaceIds.Count == 0)
            {
                IsPersonGroupTrained = false;
                return;
            }

            await faceClient.PersonGroup.TrainAsync(personGroupId);

            // TODO: add progress indicator
            TrainingStatus trainingStatus = null;
            do
            {
                trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);
                await Task.Delay(1000);
            } while (trainingStatus.Status == TrainingStatusType.Running);

            IsPersonGroupTrained =
                (trainingStatus.Status == TrainingStatusType.Succeeded) ? true : false;
        }

        private async Task<IList<string>> GetFaceImagePathsAsync(Person person)
        {
            IList<string> faceUserData = new List<string>();

            IList<Guid> persistedFaceIds = person.PersistedFaceIds;
            foreach(Guid pfid in persistedFaceIds)
            {
                PersistedFace face = await faceClient.PersonGroupPerson.GetFaceAsync(
                    personGroupId, person.PersonId, pfid);
                if (!string.IsNullOrEmpty(face.UserData))
                {
                    faceUserData.Add(face.UserData);
                    Debug.WriteLine("GetFaceImagePathsAsync: " + face.UserData);
                }
            }
            return faceUserData;
        }

        public async Task<bool> MatchFaceAsync(Guid faceId)
        {
            if((faceId == Guid.Empty) || (searchedForPerson?.PersonId == null)) { return false; }

            VerifyResult results;
            try
            {
                results = await faceClient.Face.VerifyFaceToPersonAsync(
                    faceId, searchedForPerson.PersonId, personGroupId);

            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("MatchFaceAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "No faces associated with this person");
                return false;
            }

            // Can change using VerifyResult.Confidence.
            // Default: True if similarity confidence is greater than or equal to 0.5.
            return results.IsIdentical;
        }

        public async Task DisplayFacesInGroupAsync(Person person, ObservableCollection<ImageInfo> GroupInfos)
        {
            IList<string> faceImagePaths = await GetFaceImagePathsAsync(person);
            if(faceImagePaths == Array.Empty<string>()) { return; }

            foreach (string path in faceImagePaths)
            {
                ImageInfo groupInfo = new ImageInfo();
                groupInfo.FilePath = path;
                GroupInfos.Add(groupInfo);
            }
        }

        public async Task DeletePersonGroupAsync(string name, bool askFirst = true)
        {
            if (string.IsNullOrWhiteSpace(name)) { return; }

            string personGroupId = ConfigurePersonName(name) + "-group-id";

            MessageBoxResult result;
            try
            {
                result = askFirst ?
                    MessageBox.Show("Delete " + name + " and its training images?", "Delete " + name,
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) :
                    MessageBoxResult.OK;

                if (result == MessageBoxResult.OK)
                {
                    await faceClient.PersonGroup.DeleteAsync(personGroupId);
                }
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("DeletePersonGroupAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "DeletePersonGroupAsync");
            }
            catch (Exception e)
            {
                Debug.WriteLine("DeletePersonGroupAsync: " + e.Message);
                MessageBox.Show(e.Message, "DeletePersonGroupAsync");
            }
        }

        private string ConfigurePersonName(string name)
        {
            return name.Replace(" ", "").ToLower();
        }
    }
}
