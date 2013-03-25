using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.UnitTestFramework;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using Xunit.Sdk;
using XunitContrib.Runner.ReSharper.RemoteRunner;

namespace XunitContrib.Runner.ReSharper.UnitTestProvider
{
    public class XunitTestClassElement : XunitBaseElement, IUnitTestElement, ISerializableUnitTestElement, IEquatable<XunitTestClassElement>
    {
        private readonly ProjectModelElementEnvoy projectModelElementEnvoy;
        private readonly IDeclaredElementEnvoy classElementEnvoy;

        public XunitTestClassElement(IUnitTestProvider provider, ProjectModelElementEnvoy projectModelElementEnvoy, 
            IDeclaredElementEnvoy classElementEnvoy, string id, IClrTypeName typeName, string assemblyLocation,
            IEnumerable<string> categories)
        {
            Provider = provider;
            this.projectModelElementEnvoy = projectModelElementEnvoy;
            this.classElementEnvoy = classElementEnvoy;
            Id = id;
            TypeName = typeName;
            AssemblyLocation = assemblyLocation;
            Children = new List<IUnitTestElement>();
            SetCategories(categories);

            ShortName = string.Join("+", typeName.TypeNames.Select(FormatTypeName).ToArray());
        }

        private static string FormatTypeName(TypeNameAndTypeParameterNumber typeName)
        {
            return typeName.TypeName + (typeName.TypeParametersNumber > 0 ? string.Format("`{0}", typeName.TypeParametersNumber) : string.Empty);
        }

        public void SetCategories(IEnumerable<string> categories)
        {
            Categories = UnitTestElementCategory.Create(categories);
        }

        public IUnitTestProvider Provider { get; private set; }
        public IUnitTestElement Parent { get; set; }

        public ICollection<IUnitTestElement> Children { get; private set; }

        public string ShortName { get; private set; }

        public bool Explicit
        {
            get { return !string.IsNullOrEmpty(ExplicitReason); }
        }

        public UnitTestElementState State { get; set; }

        public IProject GetProject()
        {
            return projectModelElementEnvoy.GetValidProjectElement() as IProject;
        }

        // ReSharper 6.1
        public string GetPresentation()
        {
            return GetPresentation(null);
        }

        // ReSharper 7.0
        public string GetPresentation(IUnitTestElement parent)
        {
            return ShortName;
        }

        public UnitTestNamespace GetNamespace()
        {
            return new UnitTestNamespace(TypeName.GetNamespaceName());
        }

        public UnitTestElementDisposition GetDisposition()
        {
            var element = GetDeclaredElement();
            if (element == null || !element.IsValid())
                return UnitTestElementDisposition.InvalidDisposition;

            var locations = from declaration in element.GetDeclarations()
                            let file = declaration.GetContainingFile()
                            where file != null
                            select new UnitTestElementLocation(file.GetSourceFile().ToProjectFile(),
                                                               declaration.GetNameDocumentRange().TextRange,
                                                               declaration.GetDocumentRange().TextRange);
            return new UnitTestElementDisposition(locations, this);
        }

        public IDeclaredElement GetDeclaredElement()
        {
           return classElementEnvoy.GetValidDeclaredElement();
        }

        public IEnumerable<IProjectFile> GetProjectFiles()
        {
            var declaredElement = GetDeclaredElement();
            if (declaredElement == null)
                return EmptyArray<IProjectFile>.Instance;

            return from sourceFile in declaredElement.GetSourceFiles()
                   select sourceFile.ToProjectFile();
        }

        // ReSharper 6.1
        public IList<UnitTestTask> GetTaskSequence(IList<IUnitTestElement> explicitElements)
        {
            return GetTaskSequence(explicitElements, null);
        }

        public IList<UnitTestTask> GetTaskSequence(ICollection<IUnitTestElement> explicitElements, IUnitTestLaunch launch)
        {
            return new List<UnitTestTask>
                       {
                           new UnitTestTask(null, new XunitTestAssemblyTask(AssemblyLocation)),
                           new UnitTestTask(this, new XunitTestClassTask(AssemblyLocation, TypeName.FullName, explicitElements.Contains(this)))
                       };
        }

        public string Kind
        {
            get { return "xUnit.net Test Class"; }
        }

        public IEnumerable<UnitTestElementCategory> Categories { get; private set; }

        public string ExplicitReason
        {
            // xunit doesn't support class level skip
            get { return string.Empty; }
        }

        public string Id { get; private set; }

        public string AssemblyLocation { get; set; }
        public IClrTypeName TypeName { get; private set; }

        public bool Equals(IUnitTestElement other)
        {
            return Equals(other as XunitTestClassElement);
        }

        public bool Equals(XunitTestClassElement other)
        {
            if (other == null)
                return false;

            return Equals(Id, other.Id) &&
                   Equals(TypeName, other.TypeName) &&
                   Equals(AssemblyLocation, other.AssemblyLocation);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (XunitTestClassElement)) return false;
            return Equals((XunitTestClassElement) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = (TypeName != null ? TypeName.GetHashCode() : 0);
                result = (result*397) ^ (AssemblyLocation != null ? AssemblyLocation.GetHashCode() : 0);
                result = (result*397) ^ (Id != null ? Id.GetHashCode() : 0);
                return result;
            }
        }

        public void WriteToXml(XmlElement element)
        {
            element.SetAttribute("projectId", GetProject().GetPersistentID());
            element.SetAttribute("typeName", TypeName.FullName);
        }

        internal static IUnitTestElement ReadFromXml(XmlElement parent, IUnitTestElement parentElement, ISolution solution, UnitTestElementFactory unitTestElementFactory)
        {
            var projectId = parent.GetAttribute("projectId");
            var typeName = parent.GetAttribute("typeName");

            var project = (IProject)ProjectUtil.FindProjectElementByPersistentID(solution, projectId);
            if (project == null)
                return null;
            var assemblyLocation = project.GetOutputFilePath().FullPath;

            // TODO: Save and load traits. Might not be necessary - they are reset when scanning the file
            return unitTestElementFactory.GetOrCreateTestClass(project, new ClrTypeName(typeName), assemblyLocation, new MultiValueDictionary<string, string>());
        }

        public void AddChild(XunitTestMethodElement xunitTestMethodElement)
        {
            Children.Add(xunitTestMethodElement);
        }

        public void RemoveChild(XunitTestMethodElement xunitTestMethodElement)
        {
            Children.Remove(xunitTestMethodElement);
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", GetType().Name, Id);
        }
    }
}