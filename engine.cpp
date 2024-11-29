#include <iostream>
#include <fstream>
#include <string>
#include <ctime>

void log_to_file(const std::string& message, const std::string& stream, const std::string& direction) {
    std::ofstream log_file("engine_log.txt", std::ios::app);
    time_t now = time(0);
    std::string time_str = ctime(&now);
    time_str.pop_back(); // Remove newline from ctime
    
    log_file << "[" << time_str << "] " 
             << "[" << stream << "] "
             << "[" << direction << "] "
             << message << std::endl;
    log_file.close();
}

int main() {
    // Disable buffering on all streams
    std::ios_base::sync_with_stdio(false);
    std::cin.tie(nullptr);
    std::cout.tie(nullptr);
    setvbuf(stdout, nullptr, _IONBF, 0);
    setvbuf(stderr, nullptr, _IONBF, 0);

    std::string line;
    
    log_to_file("Engine started", "system", "internal");

    while (std::getline(std::cin, line)) {
        log_to_file(line, "stdin", "received");

        // Output debug info to stderr
        std::string debug_info = "!Debug info: Processing move request";
        std::cerr << debug_info << std::endl << std::flush;
        log_to_file(debug_info, "stderr", "sent");

        // Always return a1a1 with proper line ending
        std::cout << "a1a1\n" << std::flush;
        log_to_file("a1a1", "stdout", "sent");
    }

    log_to_file("Engine shutting down", "system", "internal");
    return 0;
}