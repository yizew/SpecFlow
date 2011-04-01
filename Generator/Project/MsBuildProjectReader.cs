﻿using System;
using System.IO;
using System.Linq;
using Microsoft.Build.BuildEngine;
using TechTalk.SpecFlow.Generator.Configuration;
using TechTalk.SpecFlow.Generator.Interfaces;

namespace TechTalk.SpecFlow.Generator.Project
{
    public class MsBuildProjectReader : ISpecFlowProjectReader
    {
        private readonly ISpecFlowProjectConfigurationLoader configurationLoader;

        public MsBuildProjectReader(ISpecFlowProjectConfigurationLoader configurationLoader)
        {
            this.configurationLoader = configurationLoader;
        }

        public static SpecFlowProject LoadSpecFlowProjectFromMsBuild(string projectFilePath)
        {
            return new MsBuildProjectReader(new RuntimeSpecFlowProjectConfigurationLoader()).ReadSpecFlowProject(new FileProjectReference(projectFilePath));
        }

        public SpecFlowProject ReadSpecFlowProject(IProjectReference projectReference)
        {
            var projectFilePath = FileProjectReference.AssertFileProjectReference(projectReference).ProjectFilePath;

            var project = Engine.GlobalEngine.GetLoadedProject(projectFilePath);
            if (project == null)
            {
                project = new Microsoft.Build.BuildEngine.Project();
                project.Load(projectFilePath, ProjectLoadSettings.IgnoreMissingImports);
            }

            string projectFolder = Path.GetDirectoryName(projectFilePath);

            SpecFlowProject specFlowProject = new SpecFlowProject();
            specFlowProject.ProjectSettings.ProjectFolder = projectFolder;
            specFlowProject.ProjectSettings.ProjectName = Path.GetFileNameWithoutExtension(projectFilePath);
            specFlowProject.ProjectSettings.AssemblyName = project.GetEvaluatedProperty("AssemblyName");
            specFlowProject.ProjectSettings.DefaultNamespace = project.GetEvaluatedProperty("RootNamespace");

            var items = project.GetEvaluatedItemsByName("None").Cast<BuildItem>()
                .Concat(project.GetEvaluatedItemsByName("Content").Cast<BuildItem>());
            foreach (BuildItem item in items)
            {
                var extension = Path.GetExtension(item.FinalItemSpec);
                if (extension.Equals(".feature", StringComparison.InvariantCultureIgnoreCase))
                {
                    var featureFile = new FeatureFileInput(item.FinalItemSpec);
                    var ns = item.GetEvaluatedMetadata("CustomToolNamespace");
                    if (!String.IsNullOrEmpty(ns))
                        featureFile.CustomNamespace = ns;
                    specFlowProject.FeatureFiles.Add(featureFile);
                }

                if (Path.GetFileName(item.FinalItemSpec).Equals("app.config", StringComparison.InvariantCultureIgnoreCase))
                {
                    var configFilePath = Path.Combine(projectFolder, item.FinalItemSpec);
                    var configFileContent = File.ReadAllText(configFilePath);
                    var configurationHolder = new SpecFlowConfigurationHolder(configFileContent);
                    specFlowProject.ProjectSettings.ConfigurationHolder = configurationHolder;
                    specFlowProject.Configuration = configurationLoader.LoadConfiguration(configurationHolder, projectReference);
                }
            }
            return specFlowProject;
        }
    }
}