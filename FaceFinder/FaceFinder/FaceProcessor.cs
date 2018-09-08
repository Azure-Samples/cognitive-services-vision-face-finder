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
        private const char FACE_DELIMITER = '<';

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

        public async Task GetOrCreatePersonGroupAsync(string name,
            ObservableCollection<ImageInfo> GroupInfos)
        {
            Debug.WriteLine("GetOrCreatePersonGroupAsync: " + name);
            if (string.IsNullOrWhiteSpace(name)) { return; }

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
                    DisplayFacesInGroup(searchedForPerson.UserData, GroupInfos);
                }
                return;
            }
            catch (APIErrorException ae)
            {
                if(ae.Response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    searchedForPerson = defaultEmptyPerson;
                    personGroupId = string.Empty;
                    Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
                    MessageBox.Show(ae.Message, "GetOrCreatePersonGroupAsync");
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
                searchedForPerson = await faceClient.PersonGroupPerson.CreateAsync(
                    personGroupId, personName);
                GroupInfos.Clear();
                return;
            }
            catch (APIErrorException ae)
            {
                searchedForPerson = defaultEmptyPerson;
                await faceClient.PersonGroup.DeleteAsync(personGroupId);
                personGroupId = string.Empty;
                Debug.WriteLine("GetOrCreatePersonGroupAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "GetOrCreatePersonGroupAsync");
                return;
            }
        }

        // Each image must contain only 1 detected face.
        public async Task AddFacesToPersonGroupAsync(string personName,
            IList<ImageInfo> selectedItems, ObservableCollection<ImageInfo> GroupInfos)
        {
            if (searchedForPerson == defaultEmptyPerson)
            {
                Debug.WriteLine("AddFacesToPersonGroupAsync, no searchedForPerson, personName = " + personName);
                return;
            }

            string userData = searchedForPerson.UserData;

            foreach (ImageInfo info in selectedItems)
            {
                string imagePath = info.FilePath;

                // TODO: userData always null -> adds duplicate images
                if (userData?.Contains(imagePath) ?? false) { continue; } // Face already added to Person

                using (FileStream stream = new FileStream(info.FilePath, FileMode.Open))
                {
                    PersistedFace persistedFace =
                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(
                            personGroupId, searchedForPerson.PersonId, stream, "img123");
                    //imagePath + FACE_DELIMITER.ToString());
                }

                GroupInfos.Add(info);
            }

            searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(personGroupId))[0];

            string str = searchedForPerson.UserData ?? "null";
            Debug.WriteLine("searchedForPerson.UserData: " + str);

            if(searchedForPerson.PersistedFaceIds.Count == 0)
            {
                IsPersonGroupTrained = false; return;
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

        public void DisplayFacesInGroup(string userData, ObservableCollection<ImageInfo> GroupInfos)
        {
            string[] filePaths =
                userData?.Split(new char[] { FACE_DELIMITER }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if(filePaths == Array.Empty<string>()) { return; }

            foreach (string path in filePaths)
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
