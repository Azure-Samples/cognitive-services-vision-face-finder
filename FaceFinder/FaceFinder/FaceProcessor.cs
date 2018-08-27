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
        private const char USERDATA_DELIMITER = '<';

        private IFaceClient faceClient;

        private string personGroupId = string.Empty;
        private string personGroupName = "PersonGroup";
        private Person searchedForPerson = new Person(Guid.Empty, string.Empty, string.Empty);

        // A trained PersonGroup has at least 1 added face and has successfully completed the training process at least once.
        public bool IsPersonGroupTrained { get; private set; }

        public string PersonGroupName
        {
            get => personGroupName;
            set => SetProperty(ref personGroupName, value);
        }

        public FaceProcessor()
        {
            faceClient = ((App)Application.Current).faceClient;
        }

        public async Task<IList<PersonGroup>> GetAllPersonGroupsAsync()
        {
            try
            {
                return await faceClient.PersonGroup.ListAsync();
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetAllPersonGroupsAsync: {0}", e.Message);
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
                Debug.WriteLine("GetAllPersonGroupsNamesAsync: {0}", e.Message);
                MessageBox.Show(e.Message, "GetAllPersonGroupsAsync");
            }
            return personGroupNames;
        }

        public async Task<bool> VerifyPersonGroupAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { return false; }

            string groupName = ConfigurePersonName(name) + "-group";

            IList<string> personGroupNames = new List<string>();
            try
            {
                IList<PersonGroup> personGroups = await faceClient.PersonGroup.ListAsync();
                foreach (PersonGroup group in personGroups)
                {
                    if (group.Name.Equals(groupName))
                    {
                        personGroupId = group.PersonGroupId;
                        personGroupName = groupName;
                        searchedForPerson = (await faceClient.PersonGroupPerson.ListAsync(personGroupId))[0];
                        return true;
                    }
                }
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("VerifyPersonGroupAsync: {0}", e.Message);
                MessageBox.Show(e.Message, "VerifyPersonGroupAsync");
            }
            return false;
        }

        public async Task CreatePersonGroupAsync(string name,
            ObservableCollection<ImageInfo> GroupInfos)
        {
            if (string.IsNullOrWhiteSpace(name)) { return; }

            string personName = ConfigurePersonName(name);

            PersonGroupName = personName + "-group";

            // lowercase char, digit, '-', or '_'; maximum length 64
            personGroupId = PersonGroupName + "-id";

            PersonGroup group;
            try
            {
                group = await faceClient.PersonGroup.GetAsync(personGroupId);
                IList<Person> people = await faceClient.PersonGroupPerson.ListAsync(personGroupId);
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
                    personGroupId = string.Empty;
                    Debug.WriteLine("CreatePersonGroupAsync: {0}", ae.Message);
                    MessageBox.Show(ae.Message, "CreatePersonGroupAsync");
                    return;
                }
            }

            try
            {
                await faceClient.PersonGroup.CreateAsync(
                    personGroupId, PersonGroupName, personName);

                // Limit to 1 Person per group.
                searchedForPerson = await faceClient.PersonGroupPerson.CreateAsync(
                    personGroupId, personName, "someData");
                GroupInfos.Clear();
            }
            catch (APIErrorException ae)
            {
                personGroupId = string.Empty;
                Debug.WriteLine("CreatePersonGroupAsync: {0}", ae.Message);
                MessageBox.Show(ae.Message, "CreatePersonGroupAsync");
                return;
            }
        }

        // Each image must contain only 1 detected face.
        public async Task AddFacesToPersonGroupAsync(string personName,
            IList<ImageInfo> selectedItems, ObservableCollection<ImageInfo> GroupInfos)
        {
            if(!await VerifyPersonGroupAsync(personName)) { return; }

            string userData = searchedForPerson.UserData;

            int newFaces = 0;

            foreach (ImageInfo info in selectedItems)
            {
                string imagePath = info.FilePath;

                // Face already in Person
                if (userData?.Contains(imagePath) ?? false) { continue; }

                using (FileStream stream = new FileStream(info.FilePath, FileMode.Open))
                {
                    PersistedFace persistedFace =
                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(
                            personGroupId, searchedForPerson.PersonId, stream,
                            imagePath + USERDATA_DELIMITER.ToString());
                }

                newFaces++;
                GroupInfos.Add(info);
            }

            if(newFaces == 0)
            {
                IsPersonGroupTrained = true;
                return;
            }


            await faceClient.PersonGroup.TrainAsync(personGroupId);

            TrainingStatus trainingStatus = null;
            do
            {
                trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);
                await Task.Delay(1000);
            } while (trainingStatus.Status == TrainingStatusType.Running);

            IsPersonGroupTrained = true;
        }

        public async Task<bool> MatchFaceAsync(Guid faceId)
        {
            if((faceId == Guid.Empty) || (searchedForPerson?.PersonId == null)) { return false; }

            VerifyResult results =
                await faceClient.Face.VerifyFaceToPersonAsync(
                    faceId, searchedForPerson.PersonId, personGroupId);

            // Can change using VerifyResult.Confidence.
            // Default: True if similarity confidence is greater than or equal to 0.5.
            return results.IsIdentical;
        }

        public void DisplayFacesInGroup(string userData, ObservableCollection<ImageInfo> GroupInfos)
        {
            string[] filePaths =
                userData?.Split(new char[] { USERDATA_DELIMITER }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            foreach (string path in filePaths)
            {
                ImageInfo groupInfo = new ImageInfo();
                groupInfo.FilePath = path;
                GroupInfos.Add(groupInfo);
            }
        }

        public async Task DeletePersonGroupAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { return; }

            string personGroupId = ConfigurePersonName(name) + "-group-id";

            try
            {
                MessageBoxResult result =
                    MessageBox.Show("Delete " + name + " and its training images?", "Delete " + name,
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if(result == MessageBoxResult.OK)
                {
                    await faceClient.PersonGroup.DeleteAsync(personGroupId);
                }
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("DeletePersonGroupAsync: {0}", ae.Message);
                MessageBox.Show(ae.Message, "DeletePersonGroupAsync");
            }
            catch (Exception e)
            {
                Debug.WriteLine("DeletePersonGroupAsync: {0}", e.Message);
                MessageBox.Show(e.Message, "DeletePersonGroupAsync");
            }
        }

        private string ConfigurePersonName(string name)
        {
            return name.Replace(" ", "").ToLower();
        }
    }
}
