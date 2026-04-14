source "https://rubygems.org"
gem "fastlane"
gem "fastlane-plugin-github_action", git: "https://github.com/joshdholtz/fastlane-plugin-github_action"
plugins_path = File.join(File.dirname(__FILE__), "fastlane", "Pluginfile")
eval_gemfile(plugins_path) if File.exist?(plugins_path)
